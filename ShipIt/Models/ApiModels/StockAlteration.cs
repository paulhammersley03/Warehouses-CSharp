using ShipIt.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ShipIt.Models.ApiModels
{
    public class StockAlteration
    {
        public int ProductId { get; set; }
        public string Gtin { get; set; }
        public double Weight { get; set; }
        public int Quantity { get; set; }

        public StockAlteration(int productId, int quantity, double m_g, string gtin)
        {
            this.ProductId = productId;
            this.Quantity = quantity;
            this.Weight = m_g * quantity;
            this.Gtin = gtin;

            if (quantity < 0)
            {
                throw new MalformedRequestException("Alteration must be positive");
            }
        }
    }
}