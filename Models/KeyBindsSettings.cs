namespace EchoX.Models
{
    public class KeyBindsSettings
    {
        public string CycleKey  { get; set; } = "S";
        public string CycleMods { get; set; } = "Control, Alt";
        public string MuteKey   { get; set; } = "K";
        public string MuteMods  { get; set; } = "Control, Alt";
        public string OpenAppKey  { get; set; } = "X";
        public string OpenAppMods { get; set; } = "Shift, Alt";
        public bool IsOpenAppEnabled { get; set; } = true;
        public bool IsCycleEnabled   { get; set; } = true;
        public bool IsMuteEnabled    { get; set; } = true;
    }
}
