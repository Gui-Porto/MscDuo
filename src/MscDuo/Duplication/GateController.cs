using System.IO;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal class GateController : MonoBehaviour
    {
        internal const float CurtainZ = 0.27f;
        internal const float GuideTop = 4.55f;
        internal const float DrumZ = 0.48f;
        internal const float DrumY = 4.75f;
        internal const float Core = 0.11f;
        internal const float Wrap = 0.016f;
        internal const float Pitch = 0.1f;
        internal const float SlatHeight = 0.097f;
        internal const float BarHeight = 0.075f;
        internal const float OpenBar = 4.44f;
        internal const int SlatCount = 55;
        const float Length = SlatCount * Pitch;
        const float Spring = 190f;
        const float Damping = 5f;
        const float StartDelay = 0.15f;
        const float ReverseDelay = 0.5f;
        const float Accelerate = 0.35f;

        internal int Index;
        internal float Width;
        internal float Speed = 0.45f;
        internal float Volume = 1f;
        internal float Position;
        internal int Motion;
        internal int LastDir = -1;

        internal Transform[] Slats;
        internal Transform Bar;
        internal Transform Shaft;
        internal BoxCollider Curtain;
        internal AudioSource MotorLoop;

        float velocity;
        float sag;
        float sagVelocity;
        float startAt;
        float reverseAt;
        int pending;
        bool dirty = true;
        readonly Vector2[] points = new Vector2[SlatCount + 1];

        internal bool Busy => Motion != 0 || pending != 0 || reverseAt > 0f;

        internal Vector3 Center => transform.TransformPoint(new Vector3(Width / 2f, 2f, 0f));

        void Update()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f) return;
            var now = Time.time;
            if (pending != 0 && now >= startAt)
            {
                Motion = pending;
                pending = 0;
            }
            if (reverseAt > 0f && now >= reverseAt)
            {
                reverseAt = 0f;
                Begin(1, true);
            }

            var before = velocity;
            if (Motion != 0) velocity = Mathf.MoveTowards(velocity, Motion * Speed, Speed / Accelerate * dt);
            Position += velocity * dt;
            if (Position >= OpenBar)
            {
                Position = OpenBar;
                if (Motion > 0) Halt(false);
            }
            if (Position <= 0f)
            {
                Position = 0f;
                if (Motion < 0) Halt(false);
            }
            if (Motion < 0 && Blocked(dt))
            {
                Halt(false);
                reverseAt = now + ReverseDelay;
                WarehouseGates.Notify(this);
            }

            var accel = (velocity - before) / dt;
            sagVelocity += (-Spring * sag - Damping * sagVelocity - accel) * dt;
            sag = Mathf.Clamp(sag + sagVelocity * dt, -0.06f, 0.06f);
            if (Position + sag < 0f)
            {
                sag = -Position;
                if (sagVelocity < 0f) sagVelocity = 0f;
            }
            var settling = Mathf.Abs(sag) > 0.0002f || Mathf.Abs(sagVelocity) > 0.002f;
            if (!settling && sag != 0f)
            {
                sag = 0f;
                sagVelocity = 0f;
                dirty = true;
            }
            if (velocity != 0f || settling || dirty)
            {
                Layout();
                dirty = false;
            }
            Sounds();
        }

        internal void Command(int command)
        {
            if (command == 0)
            {
                if (!Busy) return;
                Halt(true);
                return;
            }
            Begin(command, true);
        }

        internal void Remote()
        {
            if (Busy) Command(0);
            else if (Position <= 0.001f) Command(1);
            else if (Position >= OpenBar - 0.001f) Command(-1);
            else Command(-LastDir);
        }

        void Begin(int dir, bool notify)
        {
            if (dir > 0 && Position >= OpenBar - 0.001f) return;
            if (dir < 0 && Position <= 0.001f) return;
            reverseAt = 0f;
            LastDir = dir;
            if (Motion == dir || pending == dir) return;
            var delay = StartDelay;
            if (Motion != 0)
            {
                Halt(false);
                delay = ReverseDelay;
            }
            pending = dir;
            startAt = Time.time + delay;
            if (notify) WarehouseGates.Notify(this);
        }

        void Halt(bool notify)
        {
            Motion = 0;
            pending = 0;
            reverseAt = 0f;
            velocity = 0f;
            if (notify) WarehouseGates.Notify(this);
        }

        internal void Write(BinaryWriter w)
        {
            w.Write((byte)Index);
            w.Write((sbyte)(reverseAt > 0f ? 1 : pending != 0 ? pending : Motion));
            w.Write(Position);
            w.Write((sbyte)LastDir);
        }

        internal void Apply(int motion, float position, int lastDir)
        {
            if (Mathf.Abs(position - Position) > 0.01f)
            {
                Position = Mathf.Clamp(position, 0f, OpenBar);
                dirty = true;
            }
            if (lastDir != 0) LastDir = lastDir;
            if (motion == 0)
            {
                if (Busy) Halt(false);
            }
            else Begin(motion, false);
        }

        internal void SetPosition(float position)
        {
            Position = Mathf.Clamp(position, 0f, OpenBar);
            dirty = true;
            Layout();
        }

        bool Blocked(float dt)
        {
            var root = Warehouse.Root != null ? Warehouse.Root.transform : null;
            var reach = Mathf.Min(0.07f + Speed * dt * 3f, Position + 0.015f);
            if (reach > 0.01f)
            {
                var count = Mathf.CeilToInt(Width / 0.7f);
                for (int i = 0; i < count; i++)
                {
                    var origin = transform.TransformPoint(new Vector3(Width * (i + 0.5f) / count, Position + 0.02f, CurtainZ));
                    if (Hit(origin, Vector3.down, reach, root)) return true;
                }
            }
            foreach (var height in new[] { 0.3f, 1.0f })
            {
                if (Position < height + 0.05f) continue;
                foreach (var z in new[] { CurtainZ - 0.13f, CurtainZ + 0.13f })
                    if (Hit(transform.TransformPoint(new Vector3(-0.03f, height, z)), transform.right, Width + 0.06f, root)) return true;
            }
            return false;
        }

        static bool Hit(Vector3 origin, Vector3 direction, float length, Transform root)
        {
            foreach (var hit in Physics.RaycastAll(origin, direction, length))
            {
                var c = hit.collider;
                if (c == null || c.isTrigger) continue;
                if (root != null && c.transform.IsChildOf(root)) continue;
                return true;
            }
            return false;
        }

        internal void Layout()
        {
            var barTop = Position + BarHeight;
            var hang = Mathf.Max(GuideTop - barTop, 0f);
            var guide = new Vector2(CurtainZ, GuideTop);
            var drum = new Vector2(DrumZ, DrumY);
            var radius = Core;
            Vector2 tangent;
            float theta;
            for (int i = 0; i < 6; i++)
            {
                Tangent(guide, drum, radius, out tangent, out theta);
                var wound = Mathf.Max(0f, Length - hang - (tangent - guide).magnitude);
                radius = Mathf.Sqrt(Core * Core + wound * Wrap / Mathf.PI);
            }
            Tangent(guide, drum, radius, out tangent, out theta);
            var slant = (tangent - guide).magnitude;

            for (int k = 0; k <= SlatCount; k++)
            {
                var s = k * Pitch;
                if (s <= hang)
                {
                    points[k] = new Vector2(CurtainZ, barTop + s + sag * (1f - s / Mathf.Max(hang, 0.001f)));
                    continue;
                }
                s -= hang;
                if (s <= slant)
                {
                    points[k] = Vector2.Lerp(guide, tangent, slant > 0f ? s / slant : 1f);
                    continue;
                }
                s -= slant;
                var rho = Mathf.Sqrt(Mathf.Max(radius * radius - s * Wrap / Mathf.PI, Core * Core));
                var angle = theta - 2f * Mathf.PI / Wrap * (radius - rho);
                points[k] = drum + rho * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
            for (int k = 0; k < SlatCount; k++)
            {
                var p = points[k];
                var dir = points[k + 1] - p;
                if (dir.sqrMagnitude < 1e-8f) dir = new Vector2(0f, 1f);
                dir.Normalize();
                Slats[k].localPosition = new Vector3(0f, p.y, p.x);
                Slats[k].localRotation = Quaternion.LookRotation(new Vector3(0f, -dir.x, dir.y), new Vector3(0f, dir.y, dir.x));
            }
            Bar.localPosition = new Vector3(0f, Position + sag, CurtainZ);
            var end = theta - 2f * Mathf.PI / Wrap * (radius - Core);
            Shaft.localRotation = Quaternion.AngleAxis(90f - end * Mathf.Rad2Deg, Vector3.right);
            Curtain.center = new Vector3(Width / 2f, (Position + GuideTop) / 2f, CurtainZ);
            Curtain.size = new Vector3(Width + 0.1f, Mathf.Max(GuideTop - Position, 0.05f), 0.05f);
        }

        static void Tangent(Vector2 from, Vector2 center, float radius, out Vector2 point, out float theta)
        {
            var d = from - center;
            var alpha = Mathf.Acos(Mathf.Clamp(radius / d.magnitude, -1f, 1f));
            theta = Mathf.Atan2(d.y, d.x) - alpha;
            point = center + radius * new Vector2(Mathf.Cos(theta), Mathf.Sin(theta));
        }

        void Sounds()
        {
            var frac = Mathf.Clamp01(Mathf.Abs(velocity) / Speed);
            Loop(MotorLoop, frac, 0.55f * Mathf.Sqrt(frac), 0.72f + 0.28f * frac);
        }

        void Loop(AudioSource source, float frac, float volume, float pitch)
        {
            if (source == null) return;
            if (frac > 0.001f)
            {
                source.volume = volume * Volume;
                source.pitch = pitch;
                if (!source.isPlaying) source.Play();
            }
            else if (source.isPlaying) source.Stop();
        }
    }
}
