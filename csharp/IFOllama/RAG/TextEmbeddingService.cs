// TextEmbeddingService.cs (Fully Local via ML.NET)

using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Text;

namespace IFOllama.RAG
{

    public class TextEmbeddingService : IEmbeddingService
    {
        private readonly MLContext _ml;
        private readonly ITransformer _transformer;
        private readonly PredictionEngine<RawText, TransformedEmbedding> _engine;

        public TextEmbeddingService()
        {
            _ml = new MLContext();
            var pipeline = _ml.Transforms.Text.NormalizeText("NormalizedText", nameof(RawText.Text))
                .Append(_ml.Transforms.Text.TokenizeIntoWords("Tokens", "NormalizedText"))
                .Append(_ml.Transforms.Text.ApplyWordEmbedding(
                    outputColumnName: "Embedding",
                    inputColumnName: "Tokens",
                    modelKind: WordEmbeddingEstimator.PretrainedModelKind.SentimentSpecificWordEmbedding));

            // Fit on empty data
            var empty = _ml.Data.LoadFromEnumerable([new RawText { Text = string.Empty }]);
            _transformer = pipeline.Fit(empty);
            _engine = _ml.Model.CreatePredictionEngine<RawText, TransformedEmbedding>(_transformer);
        }

        public Task<float[]> EmbedAsync(string text)
        {
            var result = _engine.Predict(new RawText { Text = text });
            return Task.FromResult(result.Embedding);
        }

        private class RawText
        {
            public string Text { get; set; } = string.Empty; 
        }

        // Fix: Remove the `required` modifier from the `Embedding` property in the `TransformedEmbedding` class.
        // This ensures that the `TransformedEmbedding` class satisfies the `new()` constraint required by `PredictionEngine`.

        private class TransformedEmbedding
        {
            [VectorType]
            public float[] Embedding { get; set; } = Array.Empty<float>();
        }

    }

}