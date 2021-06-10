using Microsoft.ML;
using RestaurantRecommender;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RestaurantRecommenderModel
{
    public class ConsumeModel
    {
        private static Lazy<PredictionEngine<ModelInput, ModelOutput>> PredictionEngine = new Lazy<PredictionEngine<ModelInput, ModelOutput>>(CreatePredictionEngine);

        public static ModelOutput Predict(ModelInput input)
        {
            ModelOutput result = PredictionEngine.Value.Predict(input);
            return result;
        }

        public static PredictionEngine<ModelInput, ModelOutput> CreatePredictionEngine()
        {
            MLContext mLContext = new MLContext();

            string modelPath = @"D:\C#\ML.NET\RestaurantRecommender\RestaurantRecommenderModel\Model\RestaurantRecommenderModel.zip";
            ITransformer mlModel = mLContext.Model.Load(modelPath, out var modelInputSchema);
            var predEngine = mLContext.Model.CreatePredictionEngine<ModelInput, ModelOutput>(mlModel);
            return predEngine;
        }
    }
}