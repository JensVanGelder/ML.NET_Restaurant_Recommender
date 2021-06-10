﻿using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;
using Microsoft.ML.Trainers.Recommender;
using Microsoft.ML.Transforms;
using System;
using System.Collections.Generic;
using System.IO;
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
                NumberOfIterations = 10,
                ApproximationRank = 200,
                Quiet = true
            };

            var trainer = mLContext.Recommendation().Trainers.MatrixFactorization(finalOptions);
            var trainingPipeLine = dataProcessingPipeline.Append(trainer);

            //TrainModelForSingleUser(mLContext, trainingDataView, trainingPipeLine);

            var crossValMetrics = mLContext.Recommendation()
                .CrossValidate(data: trainingDataView,
                estimator: trainingPipeLine,
                labelColumnName: "TotalRating");

            var averageRMSE = crossValMetrics.Average(m => m.Metrics.RootMeanSquaredError);
            var averageRSquared = crossValMetrics.Average(m => m.Metrics.RSquared);

            Console.WriteLine();
            Console.WriteLine("--- Metrics before tuning hyper parameters ---");
            Console.WriteLine($"Cross validated root mean square error: {averageRMSE:#.000}");
            Console.WriteLine($"Cross validated RSquared: {averageRSquared:#.000}");
            Console.WriteLine();

            //HyperParameterExploration(mLContext, dataProcessingPipeline, trainingDataView);

            Console.WriteLine("Training model");
            var model = trainingPipeLine.Fit(trainingDataView);

            if (!Directory.Exists("Model"))
            {
                Directory.CreateDirectory("Model");
            }

            var modelPath = "Model\\RestaurantRecommenderModel.zip";
            mLContext.Model.Save(model, trainingDataView.Schema, modelPath);

            var predictionEngine = mLContext.Model.CreatePredictionEngine<ModelInput, ModelOutput>(model);

            var prediction = predictionEngine.Predict(
                new ModelInput()
                {
                    UserId = "Cloned",
                    RestaurantName = "Restaurant Wu Zhuo Yi"
                });

            Console.WriteLine($"Predicted: {prediction.Score:#.0} for 'Restaurant Wu Zhuo Yi'");

            TrainModelForSingleUser(mLContext, trainingDataView, trainingPipeLine);
        }

        private static void HyperParameterExploration(MLContext mLContext, EstimatorChain<ValueToKeyMappingTransformer> dataProcessingPipeline, IDataView trainingDataView)
        {
            var results = new List<(double rootMeanSquaredError,
                double rSquared,
                int iterations,
                int approximationRank)>();

            for (int iterations = 5; iterations < 100; iterations += 5)
            {
                for (int approximationRank = 50; approximationRank < 250; approximationRank += 50)
                {
                    var option = new MatrixFactorizationTrainer.Options
                    {
                        MatrixColumnIndexColumnName = "UserIdEncoded",
                        MatrixRowIndexColumnName = "RestaurantNameEncoded",
                        LabelColumnName = "TotalRating",
                        NumberOfIterations = iterations,
                        ApproximationRank = approximationRank,
                        Quiet = true
                    };

                    var currentTrainer = mLContext.Recommendation().Trainers.MatrixFactorization(option);

                    var completePipeline = dataProcessingPipeline.Append(currentTrainer);

                    var crossValMetrics = mLContext.Recommendation()
                        .CrossValidate(trainingDataView,
                        completePipeline,
                        labelColumnName: "TotalRating");

                    results.Add(
                        (crossValMetrics.Average(m => m.Metrics.RootMeanSquaredError),
                        crossValMetrics.Average(m => m.Metrics.RSquared),
                        iterations,
                        approximationRank));
                }
            }
            Console.WriteLine("--- Hyper parameters and metrics ---");

            foreach (var result in results.OrderByDescending(r => r.rSquared))
            {
                Console.Write($"NumberOfIterations: {result.iterations}");
                Console.Write($" ApproximationRank: {result.approximationRank}");
                Console.Write($" RootMeanSquaredError: {result.rootMeanSquaredError}");
                Console.WriteLine($" RSquared: {result.rSquared}");
            }

            Console.WriteLine();
            Console.WriteLine("Done");
        }

        private static void TrainModelForSingleUser(MLContext mLContext, IDataView trainingDataView, EstimatorChain<MatrixFactorizationPredictionTransformer> trainingPipeLine)
        {
            Console.WriteLine("Training model");
            var model = trainingPipeLine.Fit(trainingDataView);

            //View results
            var testUserId = "U1080";

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