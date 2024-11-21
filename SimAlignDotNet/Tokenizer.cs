using Python.Runtime;
using System.Collections.Generic;

namespace SimAlign
{
    public class Tokenizer
    {
        private dynamic _tokenizer;

        public Tokenizer(string modelName)
        {
            using (Py.GIL())
            {
                dynamic transformers = Py.Import("transformers");
                _tokenizer = transformers.AutoTokenizer.from_pretrained(modelName);
            }
        }

        // Modifica per supportare batch di frasi
        public dynamic Encode(List<List<string>> sentences, bool isSplitIntoWords)
        {
            using (Py.GIL())
            {
                // Unisci le frasi in un'unica lista di stringhe
                List<string> flatSentences = new List<string>();
                foreach (var sentence in sentences)
                {
                    flatSentences.Add(string.Join(" ", sentence));
                }

                // Passa la lista di frasi
                return _tokenizer(flatSentences, is_split_into_words: isSplitIntoWords, padding: true, truncation: true, return_tensors: "pt");
            }
        }

        // Tokenizza una singola parola
        public List<string> Tokenize(string word)
        {
            using (Py.GIL())
            {
                dynamic tokens = _tokenizer.tokenize(word);
                List<string> tokenList = new List<string>();
                foreach (dynamic token in tokens)
                {
                    tokenList.Add(token.ToString());
                }
                return tokenList;
            }
        }
    }
}