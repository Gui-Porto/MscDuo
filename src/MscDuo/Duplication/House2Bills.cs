using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class House2Bills
    {
        const string MailBoxPath = "YARD/PlayerMailBox";
        const string MailBoxMeshPath = "YARD/MailBox";
        const float MailBoxDistance = 9f;

        internal static readonly BillKind Electricity = new BillKind("Systems/ElectricityBills", "EnvelopeElectricityBill", "Sheets/ElectricityBill", "Electricity", "luz");
        internal static readonly BillKind Phone = new BillKind("Systems/PhoneBills", "EnvelopePhoneBill", "Sheets/PhoneBill", "PhoneBills", "telefone");

        internal static void Setup(GameObject house)
        {
            var mailbox = GameObject.Find(MailBoxPath);
            if (mailbox == null)
            {
                ModConsole.Warning("[MSC Duo] caixa de correio da casa 1 não encontrada; casa 2 sem contas.");
                return;
            }

            var mailbox2 = CloneMailbox(house, mailbox);
            for (int i = mailbox2.transform.childCount - 1; i >= 0; i--)
            {
                var child = mailbox2.transform.GetChild(i);
                if (child.name.StartsWith("Envelope") && child.name != Electricity.Envelope && child.name != Phone.Envelope)
                    Object.DestroyImmediate(child.gameObject);
            }
            Electricity.Setup(house, mailbox, mailbox2);
            Phone.Setup(house, mailbox, mailbox2);

            SetupPower(house);
        }

        static void SetupPower(GameObject house)
        {
            if (Electricity.House2 == null) return;
            var fsm = Electricity.House2.GetComponent<PlayMakerFSM>();
            var kwh = new FsmFloat("MscDuoHouse2Kwh");
            var power = new FsmBool("MscDuoHouse2Power") { Value = true };
            fsm.AddVariable(kwh);
            fsm.AddVariable(power);
            FsmTools.RedirectGlobal(fsm, "HouseElectricityKWH", kwh);
            FsmTools.RedirectGlobal(fsm, "HouseElectricity", power);
            House2Power.Attach(kwh, power);

            var confined = FsmTools.ConfineBroadcasts(fsm, house);
            var powered = 0;
            foreach (var kept in house.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                try { powered += FsmTools.RedirectGlobal(kept, "HouseElectricity", power); }
                catch (System.Exception e) { ModConsole.Warning($"[MSC Duo] casa 2: energia não ligada em '{kept.gameObject.name}/{kept.FsmName}': {e.Message}"); }
            }
            ModConsole.Print($"<color=orange>[MSC Duo]</color> casa 2: energia própria em {powered} pontos, corte restrito à casa 2 ({confined}).");
        }

        internal static string ForceBill(bool cutoff)
        {
            if (Electricity.House2 == null) return "conta da casa 2 não está ativa";
            var vars = Electricity.House2.GetComponent<PlayMakerFSM>().FsmVariables;
            if (cutoff) vars.GetFsmFloat("WaitCutoff").Value = vars.GetFsmFloat("NextCutoff").Value;
            else
            {
                vars.GetFsmFloat("WaitBill").Value = vars.GetFsmFloat("NextBill").Value;
                if (Phone.House2 != null)
                {
                    var phone = Phone.House2.GetComponent<PlayMakerFSM>().FsmVariables;
                    phone.GetFsmFloat("WaitBill").Value = phone.GetFsmFloat("NextBill").Value;
                }
            }
            return cutoff ? "corte de luz da casa 2 forçado" : $"contas de luz e telefone da casa 2 fechadas ({House2Power.Kwh:0.###} kWh)";
        }

        static GameObject CloneMailbox(GameObject house, GameObject mailbox)
        {
            var spot = MailboxSpot(house);
            var mailboxMesh = GameObject.Find(MailBoxMeshPath);
            float clearance;
            if (Ground.TryClearance(mailbox.transform, Ground.ProbeHeight, out clearance, mailbox, mailboxMesh)) spot.y += clearance;
            var root = house.transform.parent;
            var mailbox2 = Object.Instantiate(mailbox);
            mailbox2.name = "PlayerMailBox_P2";
            mailbox2.transform.parent = root;
            mailbox2.transform.position = spot;
            var original = HouseCloner.Original != null ? HouseCloner.Original.transform.rotation : Quaternion.identity;
            mailbox2.transform.rotation = house.transform.rotation * Quaternion.Inverse(original) * mailbox.transform.rotation;

            if (mailboxMesh != null)
            {
                var mesh2 = Object.Instantiate(mailboxMesh);
                mesh2.name = "MailBox_P2";
                mesh2.transform.parent = root;
                mesh2.transform.position = mailbox2.transform.TransformPoint(mailbox.transform.InverseTransformPoint(mailboxMesh.transform.position));
                mesh2.transform.rotation = mailbox2.transform.rotation * Quaternion.Inverse(mailbox.transform.rotation) * mailboxMesh.transform.rotation;
            }
            ModConsole.Print($"<color=orange>[MSC Duo]</color> casa 2: caixa de correio em {HouseCloner.Format(spot)}.");
            return mailbox2;
        }

        static Vector3 MailboxSpot(GameObject house)
        {
            Transform door = null;
            foreach (var t in house.GetComponentsInChildren<Transform>(true))
                if (t.name == "DoorFront") { door = t; break; }
            var center = house.transform.position;
            var front = door != null ? door.position - center : house.transform.forward;
            front.y = 0f;
            front.Normalize();
            var spot = (door != null ? door.position : center) + front * MailBoxDistance;
            float ground;
            if (Ground.TryHeight(spot, 3f, out ground)) spot.y = ground;
            return spot;
        }

        internal class BillKind
        {
            readonly string systemPath;
            readonly string sheetPath;
            readonly string sheetVariable;
            readonly string label;

            internal readonly string Envelope;
            internal GameObject House1 { get; private set; }
            internal GameObject House2 { get; private set; }

            internal BillKind(string systemPath, string envelope, string sheetPath, string sheetVariable, string label)
            {
                this.systemPath = systemPath;
                Envelope = envelope;
                this.sheetPath = sheetPath;
                this.sheetVariable = sheetVariable;
                this.label = label;
            }

            internal void Setup(GameObject house, GameObject mailbox, GameObject mailbox2)
            {
                House1 = GameObject.Find(systemPath);
                if (House1 == null)
                {
                    ModConsole.Warning($"[MSC Duo] '{systemPath}' não encontrado; casa 2 sem conta de {label}.");
                    return;
                }

                House2 = Object.Instantiate(House1);
                House2.name = House1.name + "_P2";
                var fsm = House2.GetComponent<PlayMakerFSM>();
                FsmTools.EnsureLoaded(fsm);
                FsmTools.PrefixSaveTags(fsm, KeptSystems.SaveTagPrefix);

                var envelope2 = mailbox2.transform.Find(Envelope);
                fsm.FsmVariables.GetFsmGameObject("Bill").Value = envelope2 != null ? envelope2.gameObject : null;

                var retargeted = 0;
                foreach (var kept in house.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    try { retargeted += FsmTools.RetargetObject(kept, House1, House2); }
                    catch (System.Exception e) { ModConsole.Warning($"[MSC Duo] casa 2: '{kept.gameObject.name}/{kept.FsmName}' não ligado à conta de {label}: {e.Message}"); }
                }

                Hook(mailbox.transform.Find(Envelope), House1);
                Hook(envelope2, House2);
                ModConsole.Print($"<color=orange>[MSC Duo]</color> casa 2: conta de {label} própria ({retargeted} ligações).");
            }

            void Hook(Transform envelope, GameObject bills)
            {
                if (envelope == null) return;
                var fsm = envelope.GetComponent<PlayMakerFSM>();
                if (fsm == null) return;
                FsmTools.EnsureLoaded(fsm);
                fsm.FsmInject("Open bill", () => PointSheetAt(bills), false, 0);
            }

            void PointSheetAt(GameObject bills)
            {
                var sheet = GameObject.Find(sheetPath);
                if (sheet == null) return;
                var data = sheet.GetPlayMaker("Data");
                var variable = data != null ? data.FsmVariables.GetFsmGameObject(sheetVariable) : null;
                if (variable != null) variable.Value = bills;

                var pay = sheet.transform.Find("Pay");
                var button = pay != null ? pay.GetComponent<PlayMakerFSM>() : null;
                if (button == null) return;
                FsmTools.EnsureLoaded(button);
                foreach (var state in button.FsmStates)
                    foreach (var action in state.Actions)
                    {
                        var send = action as SendEvent;
                        if (send == null || send.eventTarget == null || send.sendEvent == null || send.sendEvent.Name != "GLOBALEVENT") continue;
                        send.eventTarget.gameObject.OwnerOption = OwnerDefaultOption.SpecifyGameObject;
                        send.eventTarget.gameObject.GameObject.Value = bills;
                    }
            }
        }
    }
}
