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
using System.Reflection;

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

        public interface IFanControlScript
        {
            ScriptOutput CalculateFanSpeed(IComputer computer);
        }

        private static ScriptManager instance = null;

        private Dictionary<string, ScriptItem> codeMap = new Dictionary<string, ScriptItem>();
        private Dictionary<string, IFanControlScript> codeCache = new Dictionary<string, IFanControlScript>();

        public static ScriptManager Instance
        {
            get
            {
                if (instance == null)
                    instance = new ScriptManager();

                return instance;
            }
        }

        public IComputer Computer { get; set; }

        private ScriptManager()
        {
            // Do nothing.
        }

        public void AddScript(IControl control, ScriptItem scriptItem)
        {
            string identifierStr = control.Identifier.ToString();

            if (scriptItem.Enabled)
                codeCache[identifierStr] = createObjectFromScriptCode(scriptItem.Code);

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
                IFanControlScript fanControl = createObjectFromScriptCode(code);

                fanControl.CalculateFanSpeed(Computer);
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

        public void LoadSettings(ISettings settings)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(ScriptItem));

            foreach (var device in Computer.Hardware)
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

        public void ExecuteScripts()
        {
            foreach (var scKeyValue in codeMap)
            {
                if (scKeyValue.Value.Enabled)
                {
                    ScriptOutput output;
                    try
                    {
                        if (!codeCache.ContainsKey(scKeyValue.Key))
                            codeCache[scKeyValue.Key] = createObjectFromScriptCode(scKeyValue.Value.Code);

                        output = codeCache[scKeyValue.Key].CalculateFanSpeed(Computer);
                    }
                    catch (Exception e)
                    {
                        output.ControlMode = ControlMode.Software;
                        output.FanSpeed = 100.0f;
                        output.Reason = "ERROR: " + e.Message;
                    }

                    scKeyValue.Value.LastReason = output.Reason;

                    IControl control = findControl(scKeyValue.Key.ToString());
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
            if (!codeMap.ContainsKey(identifier.ToString()))
                return;

            ScriptItem scriptItem = codeMap[identifier.ToString()];
            scriptItem.Enabled = false;
            scriptItem.LastReason = null;
        }

        //public IControl FindControlByName(string name)
        //{
        //    return (from d in Computer.Hardware
        //            from sd in d.SubHardware
        //            from s in sd.Sensors
        //            where s.Name.Equals(name)
        //            select s.Control).SingleOrDefault();
        //}

        public ISensor FindSensorByName(string name, SensorType? sensorType)
        {
            return FindSensorByName(name, sensorType, null);
        }

        public ISensor FindSensorByName(string name, SensorType? sensorType, IHardware[] hardware)
        {
            if (hardware == null)
                hardware = Computer.Hardware;

            foreach (var d in hardware)
            {
                foreach (var s in d.Sensors)
                {
                    if (sensorType.HasValue && sensorType.Value != s.SensorType)
                        continue;

                    if (s.Name.Equals(name, StringComparison.Ordinal))
                        return s;
                }

                if (d.SubHardware != null)
                {
                    ISensor sensor = FindSensorByName(name, sensorType, d.SubHardware);
                    if (sensor != null)
                        return sensor;
                }   
            }

            return null;
        }

        private IControl findControl(string identifier)
        {
            return (from d in Computer.Hardware
                    from sd in d.SubHardware
                    from s in sd.Sensors
                    where s.Control != null && s.Control.Identifier.ToString().Equals(identifier, StringComparison.Ordinal)
                    select s.Control).SingleOrDefault();
        }

        private IFanControlScript createObjectFromScriptCode(string code)
        {
            Assembly scriptAssembly = CSScript.LoadCode(code);

            return (IFanControlScript)scriptAssembly.CreateInstance("FanControl");
        }

        public void Close()
        {
            foreach (var keyValue in codeMap)
            {
                if (keyValue.Value.Enabled)
                {
                    IControl control = findControl(keyValue.Key);
                    control.SetSoftware(100.0f);
                }
            }
        }
    }
}

