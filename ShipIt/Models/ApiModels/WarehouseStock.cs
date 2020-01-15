using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using ShipIt.Models.DataModels;

namespace ShipIt.Models.ApiModels
{
    public class WarehouseStock
    {
        public int WarehouseId { get; set; }
        public int Held { get; set; }
        public int ProductId { get; set; }
        public int LowerThresh { get; set; }
        public bool Discontinued { get; set; }
        public string CompanyId { get; set; }
        public int MinimumOrderQuantity { get; set; }
        public string ProductName { get; set; }
        public string ProductCode { get; set; }
        
        public Company Company { get; set; }
            //public string CompanyName { get; set; }
            //public string Gcp { get; set; }
            //public string Addr2 { get; set; }
            //public string Addr3 { get; set; }
            //public string Addr4 { get; set; }
            //public string PostalCode { get; set; }
            //public string City { get; set; }
            //public string Tel { get; set; }
            //public string Mail { get; set; }

        public WarehouseStock(WarehouseStockDataModel dataModel)
        {
            WarehouseId = dataModel.WarehouseId;
            Held = dataModel.held;
            ProductId = dataModel.ProductId;
            LowerThresh = dataModel.lowerThresh;
            Discontinued = dataModel.Discontinued == 1;
            CompanyId = dataModel.CompanyId;
            MinimumOrderQuantity = dataModel.MinimumOrderQuantity;
            ProductName = dataModel.ProductName;
            ProductCode = dataModel.ProductCode;
            Company = new Company(dataModel);
               

        }
}
}