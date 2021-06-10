using RecommenderModel;
using RestaurantRecommender;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RestaurantRecommenderApp
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var testUserId = "U1080";

            List<TrainDataValue> trainingData = File.ReadAllLines(@"D:\C#\ML.NET\RestaurantRecommender\RecommenderModel\Model\trainingData.csv")
                .Skip(1)
                .Select(x => TrainDataValue.FromCsv(x)).ToList();

            var alreadyRatedRestaurants = trainingData
                .Where(i => i.UserId == testUserId)
                .Select(r => r.RestaurantName)
                .Distinct();

            var allRestaurantNames = trainingData
                .Select(x => x.RestaurantName)
                .Distinct()
                .Where(r => !alreadyRatedRestaurants.Contains(r));

            var scoredRestaurants =
                allRestaurantNames.Select(restName =>
                {
                    var prediction = ConsumeModel.Predict(
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