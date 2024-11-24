using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SemanticTranscriptProcessor.Common;
using SemanticTranscriptProcessor.Common._1_TextRepresentation;
using SemanticTranscriptProcessor.Common._2_Tokenizers;
using SemanticTranscriptProcessor.Common._3_Embedders;
using SemanticTranscriptProcessor.Common._3_Embedders.Local;
using SemanticTranscriptProcessor.Common.Interfaces;
using SimAlign.Core.Alignment;

namespace SimAlign.Core
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            // Registrazione del TokenizerWrapper
            services.AddScoped<ITokenizer>(provider =>
                new TokenizerWrapper(
                    TokenizerWrapper.TokenizerMethod.Bert,
                    Configuration["Tokenizer:VocabPath"]));

            // Registrazione dell'Aggreagtor
            services.AddScoped<IAggregator, MeanAggregator>();

            // Registrazione del TorchEmbedder
            services.AddScoped<IEmbeddingProvider>(provider =>
                new TorchEmbedder(
                    provider.GetService<ITokenizer>(),
                    provider.GetService<IAggregator>(),
                    Configuration.GetValue<EmbeddingGranularity>("Embedding:Granularity"),
                    Configuration["Embedding:ModelPath"],
                    Configuration.GetValue<int>("Embedding:Layer")));

            // Registrazione delle strategie di allineamento
            services.AddScoped<SentenceAligner>();

            // Registrazione di altre dipendenze se necessario
        }

        public void Configure()
        {
            // Configurazione della pipeline HTTP se necessario
        }
    }
}