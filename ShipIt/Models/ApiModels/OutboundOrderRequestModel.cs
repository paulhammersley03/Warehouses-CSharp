using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

namespace ShipIt.Models.ApiModels
{
    public class OutboundOrderRequestModel
    {
        public int WarehouseId { get; set; }//warehouse id
        public List<OrderLine> OrderLines { get; set; }//IEnumerable of strings eg: snickers, 500, barcode, weight

        public override String ToString()
        {
            return new StringBuilder()
                .AppendFormat("warehouseId: {0}, ", WarehouseId)
                .AppendFormat("orderLines: {0}", OrderLines)
                .ToString();
        }
    }
}
