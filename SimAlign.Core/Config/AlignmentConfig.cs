namespace SimAlign.Core.Config
{
    /// <summary>
    /// Configurazione per l'allineamento delle frasi.
    /// </summary>
    public class AlignmentConfig
    {
        public ModelType Model { get; set; } = ModelType.BertBaseMultilingualCased;
        public TokenType TokenType { get; set; } = TokenType.BPE;
        public float Distortion { get; set; } = 0.0f;
        public List<MatchingMethod> MatchingMethods { get; set; } = new List<MatchingMethod>
        {
            MatchingMethod.Intersection,
            MatchingMethod.MaxWeightMatch,
            MatchingMethod.IterativeMax,
            MatchingMethod.ForwardOnly,
            MatchingMethod.ReverseOnly
        };
        public DeviceType Device { get; set; } = DeviceType.CPU;
        public int Layer { get; set; } = 8;
    }

    /// <summary>
    /// Modelli di embedding supportati.
    /// </summary>
    public enum ModelType
    {
        BertBaseUncased,
        BertBaseMultilingualCased,
        BertBaseMultilingualUncased,
        XlmMlm1001280,
        RobertaBase,
        XlmRobertaBase,
        XlmRobertaLarge,
        // Aggiungi altri modelli se necessario
    }

    /// <summary>
    /// Tipi di tokenizzazione supportati.
    /// </summary>
    public enum TokenType
    {
        BPE,
        Word
    }

    /// <summary>
    /// Metodi di allineamento disponibili.
    /// </summary>
    public enum MatchingMethod
    {
        /// <summary>
        /// Allineamento tramite intersezione dei metodi forward e reverse.
        /// </summary>
        Intersection,

        /// <summary>
        /// Allineamento basato sull'algoritmo Max Weight Match (MWMF).
        /// </summary>
        MaxWeightMatch,

        /// <summary>
        /// Allineamento iterativo migliorato (IterMax).
        /// </summary>
        IterativeMax,

        /// <summary>
        /// Allineamento in direzione forward.
        /// </summary>
        ForwardOnly,

        /// <summary>
        /// Allineamento in direzione reverse.
        /// </summary>
        ReverseOnly
    }


    /// <summary>
    /// Dispositivi disponibili per l'elaborazione.
    /// </summary>
    public enum DeviceType
    {
        CPU,
        GPU
    }

    
}