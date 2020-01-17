using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

namespace ShipIt.Models.ApiModels
{
    public class TruckShipping
    {
        public List<int> ProductIds { get; set; }
        public double TruckWeight { get; set; }
        //double EmptyCapacity { get; set; }

        public TruckShipping(List<int> productIds, double truckWeight)
        {
            ProductIds = productIds;
            TruckWeight = truckWeight;
        }
    }
}