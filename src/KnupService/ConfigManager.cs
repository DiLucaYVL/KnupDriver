using System;
using System.IO;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace EmuladorKnup360
{
    public class ButtonMapping
    {
        public Dictionary<string, int> Buttons { get; set; } = new Dictionary<string, int>();

        public static ButtonMapping CreateDefault()
        {
            var map = new ButtonMapping();
            // Mapeamento padrão calibrado para controles Knup / Twin USB
            map.Buttons["A"] = 2;
            map.Buttons["B"] = 1;
            map.Buttons["X"] = 3;
            map.Buttons["Y"] = 0;
            map.Buttons["LB"] = 4;
            map.Buttons["RB"] = 5;
            map.Buttons["LT"] = 6;
            map.Buttons["RT"] = 7;
            map.Buttons["Back"] = 8;
            map.Buttons["Start"] = 9;
            map.Buttons["L3"] = 10;
            map.Buttons["R3"] = 11;
            return map;
        }
    }

    public static class ConfigManager
    {
        private static string GetConfigDirectory()
        {
            try
            {
                string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string dir = Path.Combine(programData, "KnupXbox360");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return dir;
            }
            catch
            {
                return AppDomain.CurrentDomain.BaseDirectory;
            }
        }

        public static string GetConfigPath()
        {
            return Path.Combine(GetConfigDirectory(), "config.json");
        }

        public static ButtonMapping Load()
        {
            string path = GetConfigPath();
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var config = JsonConvert.DeserializeObject<ButtonMapping>(json);
                    if (config != null && config.Buttons.Count > 0) return config;
                }
                catch (Exception) { }
            }

            // Fallback para arquivo local se existir
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            if (File.Exists(localPath))
            {
                try
                {
                    string json = File.ReadAllText(localPath);
                    var config = JsonConvert.DeserializeObject<ButtonMapping>(json);
                    if (config != null && config.Buttons.Count > 0)
                    {
                        Save(config); // Migra para ProgramData
                        return config;
                    }
                }
                catch { }
            }

            var def = ButtonMapping.CreateDefault();
            Save(def);
            return def;
        }

        public static void Save(ButtonMapping config)
        {
            try
            {
                string path = GetConfigPath();
                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(path, json);
            }
            catch (Exception)
            {
                // Fallback local
                string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(localPath, json);
            }
        }
    }
}

