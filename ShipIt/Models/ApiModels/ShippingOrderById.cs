using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

namespace ShipIt.Models.ApiModels
{
    public class ShippingOrderById
    {
        int ProductId { get; set; }
        int Quantity { get; set; }
        int TotalOrderWeight{ get; set; }  //(item weight * Qty)
    }
}