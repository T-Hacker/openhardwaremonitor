using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using OpenHardwareMonitor.Hardware;
using CSScriptLibrary;
using csscript;
using System.Diagnostics;
using System.Xml.Serialization;
using System.IO;
using System.Xml;

namespace OpenHardwareMonitor.Utilities
{
    public class ScriptManager
    {
        public class ScriptItem
        {
            public bool Enabled { get; set; }
            public string Code { get; set; }

            [NonSerialized()]
            public string LastReason;
        }

        public struct ScriptOutput
        {
            public ControlMode ControlMode;
            public float FanSpeed;
            public string Reason;
        }

        private static ScriptManager instance = null;
        
        private Dictionary<string, ScriptItem> codeMap = new Dictionary<string, ScriptItem>();
        private Dictionary<string, MethodDelegate<ScriptOutput>> codeCache = new Dictionary<string, MethodDelegate<ScriptOutput>>();

        public static ScriptManager Instance
        {
            get
            {
                if (instance == null)
                    instance = new ScriptManager();

                return instance;
            }
        }

        private ScriptManager()
        {
            // Do nothing.
        }

        public void AddScript(IControl control, ScriptItem scriptItem)
        {
            string identifierStr = control.Identifier.ToString();

            if (scriptItem.Enabled)
                codeCache[identifierStr] = CSScript.CreateFunc<ScriptOutput>(scriptItem.Code);

            codeMap[identifierStr] = scriptItem;
        }

        public ScriptItem GetScript(IControl control)
        {
            string identifierStr = control.Identifier.ToString();

            if (!codeMap.ContainsKey(identifierStr))
                return null;

            return codeMap[identifierStr];
        }

        public bool RemoveScript(IControl control)
        {
            string identifierStr = control.Identifier.ToString();

            return codeMap.Remove(identifierStr) && codeCache.Remove(identifierStr);
        }

        public bool TryCompileScript(string code, out string err)
        {
            try
            {
                CSScript.CreateFunc<ScriptOutput>(code);
            }
            catch (Exception e)
            {
                err = e.Message;

                return false;
            }

            err = null;

            return true;
        }

        public bool HasActiveScript(IControl control)
        {
            string identifierStr = control.Identifier.ToString();

            return (codeMap.ContainsKey(identifierStr) && codeMap[identifierStr].Enabled);
        }

        public void LoadSettings(ISettings settings, IComputer computer)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(ScriptItem));

            foreach (var device in computer.Hardware)
            {
                foreach (var subDevice in device.SubHardware)
                {
                    foreach (var sensor in subDevice.Sensors)
                    {
                        string identifierStr = sensor.Control.Identifier.ToString();
                        string scriptIdentifierStr = "script:" + identifierStr;
                        if (settings.Contains(scriptIdentifierStr))
                        {
                            StringReader sr = new StringReader(settings.GetValue(scriptIdentifierStr, null));
                            ScriptItem scriptItem = serializer.Deserialize(sr) as ScriptItem;
                            if (scriptItem != null)
                                codeMap[identifierStr] = scriptItem;
                        }                            
                    }
                }
            }
        }

        public void SaveSettings(ISettings settings)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(ScriptItem));

            foreach (var item in codeMap)
            {
                StringWriter strWriter = new StringWriter();
                XmlWriter xmlWriter = XmlWriter.Create(strWriter);

                serializer.Serialize(xmlWriter, item.Value);

                settings.SetValue("script:" + item.Key, strWriter.ToString());
            }
        }

        public string GetReason(Identifier identifier)
        {
            if (!codeMap.ContainsKey(identifier.ToString()))
                return string.Empty;

            return codeMap[identifier.ToString()].LastReason;
        }

        public void ExecuteScripts(IComputer computer)
        {
            foreach (var scKeyValue in codeMap)
            {
                if (scKeyValue.Value.Enabled)
                {
                    if (!codeCache.ContainsKey(scKeyValue.Key))
                        codeCache[scKeyValue.Key] = CSScript.CreateFunc<ScriptOutput>(scKeyValue.Value.Code);

                    ScriptOutput output = codeCache[scKeyValue.Key](computer);
                    scKeyValue.Value.LastReason = output.Reason;

                    IControl control = findControl(computer, scKeyValue.Key.ToString());
                    switch (output.ControlMode)
                    {
                        case ControlMode.Undefined:
                            if (control.ControlMode != ControlMode.Undefined)
                                control.SetDefault();

                            break;

                        case ControlMode.Software:
                            control.SetSoftware(Math.Max(0.0f, Math.Min(100.0f, output.FanSpeed)));
                            break;

                        case ControlMode.Default:
                            control.SetDefault();
                            break;
                    }
                }
            }
        }

        public void DisableScript(Identifier identifier)
        {
            ScriptItem scriptItem = codeMap[identifier.ToString()];
            scriptItem.Enabled = false;
            scriptItem.LastReason = null;
        }

        private IControl findControl(IComputer computer, string identifier)
        {
            foreach (var device in computer.Hardware)
            {
                foreach (var subDevice in device.SubHardware)
                {
                    foreach (var sensor in subDevice.Sensors)
                    {
                        string identifierStr = sensor.Control.Identifier.ToString();
                        if (identifierStr.Equals(identifier, StringComparison.Ordinal))
                            return sensor.Control;
                    }
                }
            }

            return null;
        }
    }
}

