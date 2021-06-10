using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RestaurantRecommenderApp
{
    public class TrainDataValue
    {
        public string UserId;
        public string RestaurantName;
        public int TotalRating;

        public static TrainDataValue FromCsv(string csvLine)
        {
            string[] values = csvLine.Split('\t');
            TrainDataValue dataValue = new TrainDataValue
            {
                UserId = Convert.ToString(values[0]),
                RestaurantName = Convert.ToString(values[1]),
                TotalRating = Convert.ToInt32(values[2]),
            };
            return dataValue;
        }
    }
}