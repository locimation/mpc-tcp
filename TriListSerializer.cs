using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro;

namespace Crestron_TCP_Buttons {
    public class TriListSerializer {

        private static String repr(Sig sig) {
            String direction = sig.IsInput ? "INPUT" : "OUTPUT";
            switch(sig.Type) {
                case eSigType.Bool:
                    return "DIGITAL_" + direction + "<" + sig.Number + ">=(" + sig.BoolValue + ")";
                case eSigType.UShort:
                    return "ANALOG_" + direction + "<" + sig.Number + ">=(" + sig.UShortValue + ")";
                case eSigType.String:
                    return "SERIAL_" + direction + "<" + sig.Number + ">=(" + sig.StringValue + ")";
                default:
                    throw new Exception("Could not represent unknown signal type.");
            }
        }

        public static String summarize(BasicTriList triList) {
            StringBuilder sb = new StringBuilder();
            foreach(Sig sig in triList.BooleanInput) { sb.Append(repr(sig) + "\n"); }
            foreach(Sig sig in triList.BooleanOutput) { sb.Append(repr(sig) + "\n"); }
            foreach(Sig sig in triList.UShortInput) { sb.Append(repr(sig) + "\n"); }
            foreach(Sig sig in triList.UShortOutput) { sb.Append(repr(sig) + "\n"); }
            foreach(Sig sig in triList.StringInput) { sb.Append(repr(sig) + "\n"); }
            foreach(Sig sig in triList.StringOutput) { sb.Append(repr(sig) + "\n"); }
            sb.Append("\n");
            return sb.ToString();
        }

    }
}