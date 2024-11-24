using Python.Runtime;

namespace SimAlign.Core.Services
{
    public class Tokenizer
    {
        private readonly dynamic _tokenizer;

        public Tokenizer(string tokenizer_model)
        {
            if (!PythonEngine.IsInitialized)
            {
                throw new InvalidOperationException("Python.NET non è stato inizializzato. Assicurati di chiamare PythonManager.Initialize() prima di usare Tokenizer.");
            }

            using (Py.GIL())
            {
                dynamic transformers = Py.Import("transformers");
                _tokenizer = transformers.AutoTokenizer.from_pretrained(tokenizer_model);
            }
        }

        // Tokenizza batch di frasi
        public List<List<string>> TokenizeSentences(List<string> sentences)
        {
            List<List<string>> tokens = new List<List<string>>();
            foreach (var sentence in sentences)
            {
                tokens.Add(Tokenize(sentence));
            }
            return tokens;
        }

        // Tokenizza una singola frase/parola
        public List<string> Tokenize(string text)
        {
            using (Py.GIL())
            {
                dynamic tokens = _tokenizer.tokenize(text);
                var tokenList = new List<string>();

                foreach (var token in tokens)
                {
                    tokenList.Add(token.ToString());
                }

                return tokenList;
            }
        }


        // Encoda batch di frasi in tensori
        public dynamic Encode(List<List<string>> sentences, bool isSplitIntoWords)
        {
            using (Py.GIL())
            {
                try
                {
                    List<string> flatSentences = sentences.Select(sentence => string.Join(" ", sentence)).ToList();
                    return _tokenizer(flatSentences, is_split_into_words: isSplitIntoWords, padding: true, truncation: true, return_tensors: "pt");
                }
                catch (PythonException ex)
                {
                    throw new InvalidOperationException("Error during tokenization or encoding.", ex);
                }
            }
        }
    }
}