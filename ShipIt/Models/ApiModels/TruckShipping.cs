using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

namespace ShipIt.Models.ApiModels
{
    public class TruckShipping
    {
        public Dictionary<int, int> ProductQuantityByIds { get; set; }
        public double TruckWeight { get; set; }

        public TruckShipping(Dictionary<int, int> productQuantities, double truckWeight)
        {
            ProductQuantityByIds = productQuantities;
            TruckWeight = truckWeight;
        }
    }
}