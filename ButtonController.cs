using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro;

namespace Crestron_TCP_Buttons {

    public enum ButtonMode {
        Momentary,
        Toggle,
        Interlock,
        Disabled
    }

    public delegate void ButtonStateEventHandler(Button button, bool state);
    public delegate void ButtonGroupEventHandler(uint groupId, String button);
    
    public class ButtonController {

        private Dictionary<uint, Button> buttons;
        private Dictionary<uint, Feedback> leds;
        private Dictionary<uint, ButtonMode> buttonConfigs;
        private Dictionary<uint, uint> buttonGroups;
        private Dictionary<uint, uint> groupStates;

        public bool Disabled;

        public event ButtonStateEventHandler ButtonStateEvent;
        public event ButtonGroupEventHandler ButtonGroupEvent;

        public ButtonController(MPC3Basic _buttons) {
            this.buttons = new Dictionary<uint,Button>();
            this.buttonConfigs = new Dictionary<uint, ButtonMode>();
            this.buttonGroups = new Dictionary<uint, uint>();
            this.leds = new Dictionary<uint, Feedback>();
            this.groupStates = new Dictionary<uint, uint>();
            foreach(KeyValuePair<uint, Button> kv in _buttons.Button as ReadOnlyCollection<uint, Button>) {
                Button button = kv.Value;
                if(button.Name == eButtonName.VolumeUp) continue;
                if(button.Name == eButtonName.VolumeDown) continue;
                this.buttons.Add(button.Number, button);
                this.buttonConfigs.Add(button.Number, ButtonMode.Momentary);
                this.buttonGroups.Add(button.Number, 0);
                this.leds.Add(button.Number, _buttons.Feedbacks[kv.Key]);
            }

            _buttons.ButtonStateChange += new ButtonEventHandler(ButtonStateChange);

        }

        void ButtonStateChange(Crestron.SimplSharpPro.GenericBase device, ButtonEventArgs args) {

            if(Disabled) return;

            if(!buttonConfigs.ContainsKey(args.Button.Number)) return;

            switch(buttonConfigs[args.Button.Number]) {
                case ButtonMode.Disabled: {
                    break;
                }
                case ButtonMode.Momentary: {
                    leds[args.Button.Number].State = (args.NewButtonState == eButtonState.Pressed);
                    ButtonStateEvent.Invoke(args.Button, args.NewButtonState == eButtonState.Pressed);
                    break;
                }
                case ButtonMode.Toggle: {
                    if(args.NewButtonState != eButtonState.Pressed)
                        return;
                    leds[args.Button.Number].State = !leds[args.Button.Number].State;
                    ButtonStateEvent.Invoke(args.Button, leds[args.Button.Number].State);
                    break;
                }
                case ButtonMode.Interlock: {

                    if(args.NewButtonState != eButtonState.Pressed)
                        return;

                    uint group = buttonGroups[args.Button.Number];
                    groupStates[group] = args.Button.Number;
                    foreach(KeyValuePair<uint, Feedback> kv in leds) {
                        if(buttonGroups[kv.Key] == group) {
                            kv.Value.State = (kv.Key == args.Button.Number);
                        }
                    }
                    ButtonGroupEvent.Invoke(group, args.Button.Name.ToString());
                    break;
                }
            }
        }

        public void SetButtonMode(uint number, ButtonMode mode, uint group) {
            buttonConfigs[number] = mode;
            buttonGroups[number] = group;

            if(mode == ButtonMode.Disabled)
                leds[number].State = false;

            if(mode == ButtonMode.Interlock && !groupStates.ContainsKey(group)) {

                groupStates.Add(group, number);

                foreach(KeyValuePair<uint, Feedback> kv in leds) {
                    if(buttonGroups[kv.Key] == group) {
                        kv.Value.State = (kv.Key == number);
                    }
                }

            }
        }

        public void SetButtonMode(uint number, ButtonMode mode) {
            SetButtonMode(number, mode, 0);
        }

        public void SetButtonState(String name, bool state) {
            leds[GetButtonID(name)].State = state;
        }

        public void SetGroupState(uint group, String name) {
            uint buttonId = GetButtonID(name);
            groupStates[group] = buttonId;
            foreach(KeyValuePair<uint, Feedback> kv in leds) {
                if(buttonGroups[kv.Key] == group) {
                    kv.Value.State = (kv.Key == buttonId);
                }
            }
        }

        private uint GetButtonID(String name) {
            foreach(KeyValuePair<uint, Button> kv in buttons) {
                if(kv.Value.Name.ToString() == name)
                    return kv.Key;
            }
            throw new IndexOutOfRangeException("No button named: " + name);
        }

        public void SetButtonMode(String name, ButtonMode mode, uint group) {
            SetButtonMode(GetButtonID(name), mode, group);
        }

        public void SetButtonMode(String name, ButtonMode mode) {
            SetButtonMode(name, mode, 0);
        }

        public bool hasButton(String name) {
            foreach(Button button in buttons.Values) {
                if(button.Name.ToString() == name)
                    return true;
            }
            return false;
        }

        public String summarizeConfig() {
            StringBuilder sb = new StringBuilder("CONFIG:\n");
            foreach(KeyValuePair<uint, Button> kv in this.buttons) {
                String mode = buttonConfigs[kv.Key].ToString().ToUpper();
                sb.Append("  BUTTON<" + kv.Value.Name + "> = " + mode);
                if(buttonConfigs[kv.Key] == ButtonMode.Interlock) {
                    sb.Append(" GROUP " + buttonGroups[kv.Key]);
                }
                sb.Append("\n");
            }
            sb.Append("\n");
            return sb.ToString();
        }

        public String summarizeState() {
            StringBuilder sb = new StringBuilder("STATE:\n");
            foreach(KeyValuePair<uint, Feedback> kv in this.leds) {
                sb.Append("  BUTTON<" + buttons[kv.Key].Name + "> = " + kv.Value.State + "\n");
            }
            foreach(KeyValuePair<uint, uint> kv in this.groupStates) {
                sb.Append("  GROUP<" + kv.Key + "> = " + buttons[kv.Value].Name + "\n");
            }
            sb.Append("\n");
            return sb.ToString();
        }

        public String summarize() {
            return summarizeConfig() + summarizeState();
        }

    }


}