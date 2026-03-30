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
        public string OpenAppMouseButton { get; set; } = string.Empty;
        public string MixerKey  { get; set; } = "V";
        public string MixerMods { get; set; } = "Control, Alt";
        public string OpenAppGesture { get; set; } = "Alt+Shift+X";
        public string CycleGesture   { get; set; } = "Control+Alt+S";
        public string CycleMouseButton { get; set; } = string.Empty;
        public string MuteGesture    { get; set; } = "Control+Alt+K";
        public string MuteMouseButton { get; set; } = string.Empty;
        public string MixerGesture   { get; set; } = "Control+Alt+V";
        public bool IsOpenAppEnabled { get; set; } = true;
        public bool IsCycleEnabled   { get; set; } = true;
        public bool IsMuteEnabled    { get; set; } = true;
        public bool IsMixerEnabled   { get; set; } = true;
    }
}
