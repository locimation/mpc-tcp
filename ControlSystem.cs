using System;
using Crestron.SimplSharp;                          	// For Basic SIMPL# Classes
using Crestron.SimplSharpPro;                       	// For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading
using Crestron.SimplSharpPro.Diagnostics;		    	// For System Monitor Access
using Crestron.SimplSharpPro.DeviceSupport;
using System.Text.RegularExpressions;         	// For Generic Device Support

namespace Crestron_TCP_Buttons {
    public class ControlSystem : CrestronControlSystem {

        private Server server;
        private MPC3Basic panel;

        private ButtonController buttonController;

        public static ControlSystem System;

        public ControlSystem() : base() {

            Thread.MaxNumberOfUserThreads = 20;

            CrestronEnvironment.ProgramStatusEventHandler += new ProgramStatusEventHandler(ControlSystem_ControllerProgramEventHandler);
            CrestronEnvironment.EthernetEventHandler += new EthernetEventHandler(ControlSystem_ControllerEthernetEventHandler);

            System = this;
            server = new Server();

            server.ClientConnectedEvent += new ClientConnected(server_ClientConnectedEvent);
            server.ClientDataReceiveEvent += new ClientDataReceived(server_ClientDataReceiveEvent);

        }

        private static Regex ConfigureCommand = new Regex(
            @"CONFIGURE ([^ ]+) (TOGGLE|MOMENTARY|DISABLED|INTERLOCK GROUP (\d+))",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        private static Regex SetButtonCommand = new Regex(
            @"SET ([^ ]+) (TRUE|FALSE)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        private static Regex SetGroupCommand = new Regex(
            @"SETGROUP (\d+) (.+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        private static Regex SetVolumeCommand = new Regex(
            @"SETVOLUME (\d+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        private void server_ClientDataReceiveEvent(Client client, string data) {

            if(data.ToUpper() == "POLL") {
                client.Send(buttonController.summarize());
                return;
            }

            if(data.ToUpper() == "ENABLE") {
                SetBacklight(true);
                SetBeep(true);
                buttonController.Disabled = false;
                if(this.MPC3x30xTouchscreenSlot != null)
                    this.MPC3x30xTouchscreenSlot.EnableVolumeControl();
                client.Send("PANEL ENABLED\n\n");
                return;
            }

            if(data.ToUpper() == "DISABLE") {
                SetBacklight(false);
                SetBeep(false);
                buttonController.Disabled = true;
                if(this.MPC3x30xTouchscreenSlot != null)
                    this.MPC3x30xTouchscreenSlot.DisableVolumeControl();
                client.Send("PANEL DISABLED\n\n");
                return;
            }

            var configCmdMatches = ConfigureCommand.Match(data);
            if(configCmdMatches.Success) {
                String mode = configCmdMatches.Groups[2].ToString().ToUpper();
                if(mode == "MOMENTARY" && buttonController.hasButton(configCmdMatches.Groups[1].Value)) {
                    buttonController.SetButtonMode(configCmdMatches.Groups[1].Value, ButtonMode.Momentary);
                    client.Send("CONFIGURATION SUCCESS\n\n");

                } else if(mode == "TOGGLE" && buttonController.hasButton(configCmdMatches.Groups[1].Value)) {
                    buttonController.SetButtonMode(configCmdMatches.Groups[1].Value, ButtonMode.Toggle);
                    client.Send("CONFIGURATION SUCCESS\n\n");

                } else if(mode.StartsWith("INTERLOCK GROUP") && buttonController.hasButton(configCmdMatches.Groups[1].Value)) {
                    buttonController.SetButtonMode(configCmdMatches.Groups[1].Value, ButtonMode.Interlock, uint.Parse(configCmdMatches.Groups[3].Value));
                    client.Send("CONFIGURATION SUCCESS\n\n");

                } else if(mode == "DISABLED" && buttonController.hasButton(configCmdMatches.Groups[1].Value)) {
                    buttonController.SetButtonMode(configCmdMatches.Groups[1].Value, ButtonMode.Disabled);
                    client.Send("CONFIGURATION SUCCESS\n\n");

                } else {
                    client.Send("! INVALID COMMAND\n\n");
                }
                return;
            }

            var setButtonCmdMatches = SetButtonCommand.Match(data);
            if(setButtonCmdMatches.Success && setButtonCmdMatches.Groups.Count > 2) {
                String newState = setButtonCmdMatches.Groups[2].Value.ToUpper();
                if(newState == "TRUE") {
                    buttonController.SetButtonState(setButtonCmdMatches.Groups[1].Value, true);
                } else {
                    buttonController.SetButtonState(setButtonCmdMatches.Groups[1].Value, false);
                }
                client.Send("UPDATED\n\n");
                return;
            }

            var setGroupCmdMatches = SetGroupCommand.Match(data);
            if(setGroupCmdMatches.Success && setGroupCmdMatches.Groups.Count > 2) {
                String newState = setGroupCmdMatches.Groups[2].Value;
                buttonController.SetGroupState(uint.Parse(setGroupCmdMatches.Groups[1].Value), newState);
                client.Send("UPDATED\n\n");
                return;
            }

            var setVolumeCmdMatches = SetVolumeCommand.Match(data);
            if(setVolumeCmdMatches.Success && setVolumeCmdMatches.Groups.Count > 1) {
                if(this.MPC3x30xTouchscreenSlot != null)
                    this.MPC3x30xTouchscreenSlot.VolumeFeedback.UShortValue = ushort.Parse(setVolumeCmdMatches.Groups[1].Value);
                else
                    this.panel.VolumeBargraph.UShortValue = ushort.Parse(setVolumeCmdMatches.Groups[1].Value);
                
                client.Send("UPDATED\n\n");
                return;
            }

            client.Send("! UNRECOGNIZED COMMAND\n\n");
        }

        void server_ClientConnectedEvent(Client client) {
            client.Send("# Welcome to " + ControlSystem.System.ControllerPrompt + "\n\n");
            if(panel == null) {
                client.Send("! Device platform not supported.\n");
            } else {
                client.Send(buttonController.summarize());
            }
        }

        private void SetBacklight(bool on) {
            panel.LEDBrightness.UShortValue = on ? ushort.MaxValue : (ushort) 0;

            if(this.MPC3x30xTouchscreenSlot != null) {
                this.MPC3x30xTouchscreenSlot.LedBrightnessHighLevel.UShortValue = on ? ushort.MaxValue : (ushort) 0;
                this.MPC3x30xTouchscreenSlot.LedBrightnessLowLevel.UShortValue = on ? ushort.MaxValue : (ushort) 0;
            } else {
                panel.ActiveBrightness.UShortValue = on ? ushort.MaxValue : (ushort) 0;
                panel.ActiveModeAutoBrightnessHighLevel.UShortValue = on ? ushort.MaxValue : (ushort) 0;
                panel.ActiveModeAutoBrightnessLowLevel.UShortValue = on ? ushort.MaxValue : (ushort) 0; ;
                panel.StandbyBrightness.UShortValue = on ? ushort.MaxValue : (ushort) 0;
                panel.StandbyModeAutoBrightnessHighLevel.UShortValue = on ? ushort.MaxValue : (ushort) 0;
                panel.StandbyModeAutoBrightnessLowLevel.UShortValue = on ? ushort.MaxValue : (ushort) 0;
                panel.StandbyTimeout.UShortValue = 0;
            }
        }

        private void SetBeep(bool on) {
            if(on) {
                if(this.MPC3x101TouchscreenSlot != null)
                    this.MPC3x101TouchscreenSlot.EnableButtonPressBeeping();
                if(this.MPC3x102TouchscreenSlot != null)
                    this.MPC3x102TouchscreenSlot.EnableButtonPressBeeping();
                if(this.MPC3x201TouchscreenSlot != null)
                    this.MPC3x201TouchscreenSlot.EnableButtonPressBeeping();
            } else {
                if(this.MPC3x101TouchscreenSlot != null)
                    this.MPC3x101TouchscreenSlot.DisableButtonPressBeeping();
                if(this.MPC3x102TouchscreenSlot != null)
                    this.MPC3x102TouchscreenSlot.DisableButtonPressBeeping();
                if(this.MPC3x201TouchscreenSlot != null)
                    this.MPC3x201TouchscreenSlot.DisableButtonPressBeeping();
            }
        }

        private void ConfigureBasicPanel(MPC3Basic mpc) {

            mpc.Register();

            mpc.EnableMuteButton();
            mpc.EnablePowerButton();

            for(uint i = 1; i < 10; i++) {
                try { mpc.EnableNumericalButton(i); } catch(IndexOutOfRangeException) { }
            }

            mpc.AutoBrightnessEnabled.BoolValue = false;

            panel = mpc;
            SetBacklight(true);
            SetBeep(true);

            buttonController = new ButtonController(mpc);

            buttonController.ButtonStateEvent += new ButtonStateEventHandler(buttonController_ButtonStateEvent);
            buttonController.ButtonGroupEvent += new ButtonGroupEventHandler(buttonController_ButtonGroupEvent);

        }

        //private void ConfigureVolumeButtons(

        void buttonController_ButtonGroupEvent(uint groupId, string button) {
            server.Broadcast("UPDATE:\n  GROUP<" + groupId + "> = " + button + "\n\n");
        }

        void buttonController_ButtonStateEvent(Button button, bool state) {
            server.Broadcast("UPDATE:\n  BUTTON<" + button.Name.ToString() + "> = " + state + "\n\n");
        }

        public override void InitializeSystem() {
            try {

                if(this.MPC3x101TouchscreenSlot != null) {
                    ConfigureBasicPanel(this.MPC3x101TouchscreenSlot);
                    this.MPC3x101TouchscreenSlot.EnableVolumeUpButton();
                    this.MPC3x101TouchscreenSlot.EnableVolumeDownButton();
                    this.MPC3x101TouchscreenSlot.EnableProximityWakeup.BoolValue = false;
                    this.MPC3x101TouchscreenSlot.ButtonStateChange += new ButtonEventHandler(ButtonStateChange);
                }

                if(this.MPC3x102TouchscreenSlot != null) {
                    ConfigureBasicPanel(this.MPC3x102TouchscreenSlot);
                    this.MPC3x102TouchscreenSlot.EnableVolumeUpButton();
                    this.MPC3x102TouchscreenSlot.EnableVolumeDownButton();
                    this.MPC3x102TouchscreenSlot.EnableProximityWakeup.BoolValue = false;
                    this.MPC3x102TouchscreenSlot.ButtonStateChange += new ButtonEventHandler(ButtonStateChange);
                }

                if(this.MPC3x201TouchscreenSlot != null) {
                    ConfigureBasicPanel(this.MPC3x201TouchscreenSlot);
                    this.MPC3x201TouchscreenSlot.EnableVolumeUpButton();
                    this.MPC3x201TouchscreenSlot.EnableVolumeDownButton();
                    this.MPC3x201TouchscreenSlot.EnableProximityWakeup.BoolValue = false;
                    this.MPC3x201TouchscreenSlot.ButtonStateChange += new ButtonEventHandler(ButtonStateChange);
                }

                if(this.MPC3x30xTouchscreenSlot != null) {
                    ConfigureBasicPanel(this.MPC3x30xTouchscreenSlot);
                    this.MPC3x30xTouchscreenSlot.EnableVolumeControl();
                    this.MPC3x30xTouchscreenSlot.LedBrightnessHighLevel.UShortValue = ushort.MaxValue;
                    this.MPC3x30xTouchscreenSlot.LedBrightnessLowLevel.UShortValue = ushort.MaxValue;
                }

                if(panel != null) {
                    panel.SigChange += new SigEventHandler(SigChange);
                }

                server.Start();

            } catch(Exception e) {
                ErrorLog.Error("Error in InitializeSystem: {0}", e.Message);
            }
        }

        private UShortInputSig Volume {
            get {
                if(this.MPC3x101TouchscreenSlot != null)
                    return this.MPC3x101TouchscreenSlot.VolumeBargraph;
                else if(this.MPC3x102TouchscreenSlot != null)
                    return this.MPC3x102TouchscreenSlot.VolumeBargraph;
                else if(this.MPC3x201TouchscreenSlot != null)
                    return this.MPC3x201TouchscreenSlot.VolumeBargraph;
                return null;
            }
        }

        void ButtonStateChange(GenericBase device, ButtonEventArgs args) {
            if(buttonController.Disabled) return;
            if(args.Button.Name == eButtonName.VolumeUp) {
                if(Volume != null) Volume.UShortValue = (ushort) Math.Min(65535, ((int) Volume.UShortValue) + 5459);
                server.Broadcast("UPDATE:\n  VOLUME = " + Volume.UShortValue + "\n\n");
            } else if(args.Button.Name == eButtonName.VolumeDown) {
                if(Volume != null) Volume.UShortValue = (ushort) Math.Max(0, ((int) Volume.UShortValue) - 5459);
                server.Broadcast("UPDATE:\n  VOLUME = " + Volume.UShortValue + "\n\n");
            }
        }

        private ushort prevVolume = 0;

        void SigChange(BasicTriList currentDevice, SigEventArgs args) {
            if(this.MPC3x30xTouchscreenSlot != null) {
                if(args.Sig.Type == eSigType.UShort && args.Sig.Number == this.MPC3x30xTouchscreenSlot.Volume.Number) {
                    this.MPC3x30xTouchscreenSlot.VolumeFeedback.UShortValue = this.MPC3x30xTouchscreenSlot.Volume.UShortValue;
                    if(args.Sig.UShortValue != prevVolume)
                        server.Broadcast("UPDATE:\n  VOLUME = " + args.Sig.UShortValue + "\n\n");
                    prevVolume = args.Sig.UShortValue;
                    return;
                }
            }
        }

        public String stateSummary() {
            if(this.panel == null) { return ""; }
            return TriListSerializer.summarize(this.panel);
        }

        void ControlSystem_ControllerEthernetEventHandler(EthernetEventArgs ethernetEventArgs) {
            switch(ethernetEventArgs.EthernetEventType) {
                case (eEthernetEventType.LinkDown):
                    if(ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter) {
                        server.Stop();
                    }
                    break;
                case (eEthernetEventType.LinkUp):
                    if(ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter) {
                        server.Start();
                    }
                    break;
            }
        }

        void ControlSystem_ControllerProgramEventHandler(eProgramStatusEventType programStatusEventType) {
            if(programStatusEventType == eProgramStatusEventType.Stopping) {
                server.Stop();
            }
        }

    }
}