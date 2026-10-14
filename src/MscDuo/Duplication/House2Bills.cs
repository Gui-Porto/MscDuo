using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class House2Bills
    {
        const string BillsPath = "Systems/ElectricityBills";
        const string MailBoxPath = "YARD/PlayerMailBox";
        const string MailBoxMeshPath = "YARD/MailBox";
        const string Envelope = "EnvelopeElectricityBill";
        const string SheetPath = "Sheets/ElectricityBill";
        const float MailBoxDistance = 9f;

        static GameObject house1Bills;
        static GameObject house2Bills;

        internal static void Setup(GameObject house)
        {
            house1Bills = GameObject.Find(BillsPath);
            var mailbox = GameObject.Find(MailBoxPath);
            var mailboxMesh = GameObject.Find(MailBoxMeshPath);
            if (house1Bills == null || mailbox == null)
            {
                ModConsole.Warning("[MSC Duo] sistema de contas não encontrado; casa 2 sem conta de luz.");
                return;
            }

            var spot = MailboxSpot(house).Key;
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

            GameObject envelope2 = null;
            for (int i = mailbox2.transform.childCount - 1; i >= 0; i--)
            {
                var child = mailbox2.transform.GetChild(i);
                if (child.name == Envelope) envelope2 = child.gameObject;
                else if (child.name.StartsWith("Envelope")) Object.DestroyImmediate(child.gameObject);
            }

            house2Bills = Object.Instantiate(house1Bills);
            house2Bills.name = "ElectricityBills_P2";
            var fsm = house2Bills.GetComponent<PlayMakerFSM>();
            FsmTools.EnsureLoaded(fsm);
            FsmTools.PrefixSaveTags(fsm, KeptSystems.SaveTagPrefix);

            var kwh = new FsmFloat("MscDuoHouse2Kwh");
            var power = new FsmBool("MscDuoHouse2Power") { Value = true };
            fsm.AddVariable(kwh);
            fsm.AddVariable(power);
            var redirected = FsmTools.RedirectGlobal(fsm, "HouseElectricityKWH", kwh) + FsmTools.RedirectGlobal(fsm, "HouseElectricity", power);
            fsm.FsmVariables.GetFsmGameObject("Bill").Value = envelope2;
            House2Power.Attach(kwh, power);

            HookEnvelope(mailbox.transform.Find(Envelope), house1Bills);
            HookEnvelope(envelope2 != null ? envelope2.transform : null, house2Bills);

            ModConsole.Print($"<color=orange>[MSC Duo]</color> casa 2: conta de luz própria ({redirected} ligações desviadas), caixa de correio em {HouseCloner.Format(spot)}.");
        }

        internal static string ForceBill(bool cutoff)
        {
            if (house2Bills == null) return "conta da casa 2 não está ativa";
            var fsm = house2Bills.GetComponent<PlayMakerFSM>();
            var vars = fsm.FsmVariables;
            if (cutoff) vars.GetFsmFloat("WaitCutoff").Value = vars.GetFsmFloat("NextCutoff").Value;
            else vars.GetFsmFloat("WaitBill").Value = vars.GetFsmFloat("NextBill").Value;
            return cutoff ? "corte de luz da casa 2 forçado" : $"conta da casa 2 fechada com {House2Power.Kwh:0.###} kWh";
        }

        static void HookEnvelope(Transform envelope, GameObject bills)
        {
            if (envelope == null) return;
            var fsm = envelope.GetComponent<PlayMakerFSM>();
            if (fsm == null) return;
            FsmTools.EnsureLoaded(fsm);
            fsm.FsmInject("Open bill", () => PointSheetAt(bills), false, 0);
        }

        static void PointSheetAt(GameObject bills)
        {
            var sheet = GameObject.Find(SheetPath);
            if (sheet == null) return;
            var data = sheet.GetPlayMaker("Data");
            if (data != null) data.FsmVariables.GetFsmGameObject("Electricity").Value = bills;

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

        static System.Collections.Generic.KeyValuePair<Vector3, Quaternion> MailboxSpot(GameObject house)
        {
            Transform door = null;
            foreach (var t in house.GetComponentsInChildren<Transform>(true))
                if (t.name == "DoorFront") { door = t; break; }
            var center = house.transform.position;
            var front = door != null ? door.position - center : house.transform.forward;
            front.y = 0f;
            front.Normalize();
            var basePoint = door != null ? door.position : center;
            var spot = basePoint + front * MailBoxDistance;
            RaycastHit hit;
            if (Physics.Raycast(spot + Vector3.up * 20f, Vector3.down, out hit, 50f)) spot.y = hit.point.y;
            return new System.Collections.Generic.KeyValuePair<Vector3, Quaternion>(spot, Quaternion.LookRotation(-front));
        }
    }
}
