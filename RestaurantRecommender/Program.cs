using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;
using System;
using System.Linq;

namespace RestaurantRecommender
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("Restaurants recommender");

            MLContext mLContext = new MLContext(0);

            var trainingDataFile = "Data\\trainingData.tsv";
            DataPreparer.PreprocessData(trainingDataFile);

            IDataView trainingDataView = mLContext
                                           .Data
                                           .LoadFromTextFile<ModelInput>
                                           (trainingDataFile, hasHeader: true);

            var dataProcessingPipeline =
                mLContext
                    .Transforms
                    .Conversion
                    .MapValueToKey(outputColumnName: "UserIdEncoded",
                                    inputColumnName: nameof(ModelInput.UserId))
                .Append(mLContext
                    .Transforms
                    .Conversion
                    .MapValueToKey(outputColumnName: "RestaurantNameEncoded",
                                    inputColumnName: nameof(ModelInput.RestaurantName)));

            var finalOptions = new MatrixFactorizationTrainer.Options
            {
                MatrixColumnIndexColumnName = "UserIdEncoded",
                MatrixRowIndexColumnName = "RestaurantNameEncoded",
                LabelColumnName = "TotalRating",
                NumberOfIterations = 100,
                ApproximationRank = 100,
                Quiet = true
            };

            var trainer = mLContext.Recommendation().Trainers.MatrixFactorization(finalOptions);
            var trainingPipeLine = dataProcessingPipeline.Append(trainer);

            Console.WriteLine("Training model");
            var model = trainingPipeLine.Fit(trainingDataView);

            //View results
            var testUserId = "U1134";

            var predictionEngine = mLContext.Model.CreatePredictionEngine<ModelInput, ModelOutput>(model);

            var alreadyRatedRestaurants =
                mLContext
                .Data
                .CreateEnumerable<ModelInput>(trainingDataView, false)
                .Where(i => i.UserId == testUserId)
                .Select(r => r.RestaurantName)
                .Distinct();

            var allRestaurantNames =
                trainingDataView
                .GetColumn<string>("RestaurantName")
                .Distinct()
                .Where(r => !alreadyRatedRestaurants.Contains(r));

            var scoredRestaurants =
                allRestaurantNames.Select(restName =>
                {
                    var prediction = predictionEngine.Predict(
                        new ModelInput()
                        {
                            RestaurantName = restName,
                            UserId = testUserId
                        });
                    return (RestaurantName: restName, PredictedRating: prediction.Score);
                });

            var top10Restaurants = scoredRestaurants
                .OrderByDescending(s => s.PredictedRating)
                .Take(10);

            Console.WriteLine();
            Console.WriteLine($"Top 10 restaurants for {testUserId}");
            Console.WriteLine("-------------------------------------");

            foreach (var input in top10Restaurants)
            {
                Console.WriteLine($"Predicted rating [{input.PredictedRating:#.0}] for restaurant: {input.RestaurantName}");
            }
        }
    }
}