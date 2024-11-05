namespace JackboxGPT3.Services;

public class ManagedConfigFile
{
    public GeneralBlock General { get; set; }
    public BlatherRoundBlock BlatherRound { get; set; }
    public FibbageBlock Fibbage { get; set; }
    public JokeBoatBlock JokeBoat { get; set; }
    public QuiplashBlock Quiplash { get; set; }
    public SurveyScrambleBlock SurveyScramble { get; set; }
    public SurviveTheInternetBlock SurviveTheInternet { get; set; }
    public WordSpudBlock WordSpud { get; set; }

    public ManagedConfigFile()
    {
        General = new GeneralBlock();
        BlatherRound = new BlatherRoundBlock();
        Fibbage = new FibbageBlock();
        JokeBoat = new JokeBoatBlock();
        Quiplash = new QuiplashBlock();
        SurveyScramble = new SurveyScrambleBlock();
        SurviveTheInternet = new SurviveTheInternetBlock();
        WordSpud = new WordSpudBlock();
    }

    public class GeneralBlock
    {
        public string Name { get; set; }
        public string LoggingLevel { get; set; }  // verbose, debug, information, warning, error, fatal
        public string Engine { get; set; }

        public GeneralBlock()
        {
            Name = "GPT";
            LoggingLevel = "information";
            Engine = "davinci-002";
        }
    }

    public class BlatherRoundBlock
    {
        public int MaxRetries { get; set; }
        public float GenTemp { get; set; }
        public int GuessDelayMs { get; set; }
        public int SentenceDelayMs { get; set; }
        public int SkipDelayMs { get; set; }
        public int WordDelayMs { get; set; }
        public int PartDelayMs { get; set; }

        public BlatherRoundBlock()
        {
            MaxRetries = 5;
            GenTemp = 0.7f;
            GuessDelayMs = 3000;
            SentenceDelayMs = 10000;
            SkipDelayMs = 1000;
            WordDelayMs = 100;
            PartDelayMs = 500;
        }
    }

    public class FibbageBlock
    {
        public int MaxRetries { get; set; }
        public float GenTemp { get; set; }
        public float VoteTemp { get; set; }
        public int SubmissionRetries { get; set; }
        public int CategoryChoiceDelayMs { get; set; }

        public FibbageBlock()
        {
            MaxRetries = 5;
            GenTemp = 0.8f;
            VoteTemp = 0.8f;
            SubmissionRetries = 1;
            CategoryChoiceDelayMs = 3000;
        }
    }

    public class JokeBoatBlock
    {
        public int MaxRetries { get; set; }
        public float GenTemp { get; set; }
        public float VoteTemp { get; set; }
        public int MaxTopicGenCount { get; set; }

        public JokeBoatBlock()
        {
            MaxRetries = 5;
            GenTemp = 0.7f;
            VoteTemp = 0.8f;
            MaxTopicGenCount = 5;
        }
    }

    public class QuiplashBlock
    {
        public int MaxRetries { get; set; }
        public float GenTemp { get; set; }
        public float VoteTemp { get; set; }

        public QuiplashBlock()
        {
            MaxRetries = 5;
            GenTemp = 0.7f;
            VoteTemp = 0.8f;
        }
    }

    public class SurveyScrambleBlock
    {
        public int MaxRetries { get; set; }
        public float GenTemp { get; set; }
        public float VoteTemp { get; set; }

        public int TeamSelectionLockDelayMs { get; set; }
        public int ResponseMinDelayMs { get; set; }
        public int SpeedResponseMaxDelayMs { get; set; }
        public int SpeedGenFailDelayMs { get; set; }

        public SurveyScrambleBlock()
        {
            MaxRetries = 4;
            GenTemp = 0.8f;
            VoteTemp = 0.7f;

            TeamSelectionLockDelayMs = 5000;
            ResponseMinDelayMs = 2000;
            SpeedResponseMaxDelayMs = 4000;
            SpeedGenFailDelayMs = 10000;
        }
    }

    public class SurviveTheInternetBlock
    {
        public int MaxRetries { get; set; }
        public float GenTemp { get; set; }

        public SurviveTheInternetBlock()
        {
            MaxRetries = 5;
            GenTemp = 0.7f;
        }
    }

    public class WordSpudBlock
    {
        public int MaxRetries { get; set; }
        public float GenTemp { get; set; }
        public int VoteDelayMs { get; set; }

        public WordSpudBlock()
        {
            MaxRetries = 5;
            GenTemp = 0.8f;
            VoteDelayMs = 1000;
        }
    }
}
