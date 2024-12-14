using Newtonsoft.Json;

namespace JackboxGPT.Games.SurveyScramble.Models
{
    public struct ChooseOptionRequest
    {
        public ChooseOptionRequest(int option, bool doubleDown)
        {
            OptionIndex = option;
            DoubledDown = doubleDown;
        }

        [JsonProperty("optionIndex")]
        public int OptionIndex { get; set; }

        [JsonProperty("doubledDown")]
        public bool DoubledDown { get; set; }
    }
}
