using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using ShipIt.Exceptions;
using ShipIt.Models.ApiModels;
using ShipIt.Repositories;

namespace ShipIt.Controllers
{
    public class OutboundOrderController : ApiController
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private readonly IStockRepository stockRepository;
        private readonly IProductRepository productRepository;

        public OutboundOrderController(IStockRepository stockRepository, IProductRepository productRepository)
        {
            this.stockRepository = stockRepository;
            this.productRepository = productRepository;
        }

        public List<TruckShipping> Post([FromBody]OutboundOrderRequestModel request)
        {
            log.Info(String.Format("Processing outbound order: {0}", request));

            var productNumbers = CreateProductNumbers(request);//in a new method at bottom of page
            var productDataModels = productRepository.GetProductsByGtin(productNumbers);//requests data from db by product number(gtins)
            var products = productDataModels.ToDictionary(p => p.Gtin, p => new Product(p));//puts above into dictionary
            var orderItems = new List<StockAlteration>();
            var productIds = new List<int>();
            var errors = new List<string>();

            foreach (var orderLine in request.OrderLines)
            {
                if (!products.ContainsKey(orderLine.gtin))
                {
                    errors.Add(string.Format("Unknown product gtin: {0}", orderLine.gtin));
                }
                else
                {
                    var product = products[orderLine.gtin];
                    orderItems.Add(new StockAlteration(product.Id, orderLine.quantity, product.Weight, product.Gtin));
                    productIds.Add(product.Id);
                }
            }

            if (errors.Count > 0)
            {
                throw new NoSuchEntityException(string.Join("; ", errors));
            }

            CheckStockInWarehouse(request.WarehouseId, orderItems);
            stockRepository.RemoveStock(request.WarehouseId, orderItems);
            
            return TruckLoading(orderItems);
        }

        public static List<TruckShipping> TruckLoading(List<StockAlteration> orderItems)
        {
            TruckShipping truck;
            double truckWeight=0;
            List<int> productList=new List<int>();
            List<TruckShipping> truckList=new List<TruckShipping>();
            
            foreach(var order in orderItems)
            {
                var productId = order.ProductId;
                double orderWeight = 0;

                orderWeight = order.Weight * order.Quantity;

                if (orderWeight <= 2000000 - truckWeight)
                {
                    for(int i = 1; i <= orderItems.Count; i ++)
                    {
                        truckWeight += orderWeight;
                        productList.Add(productId);
                        truck = new TruckShipping(productList, truckWeight);
                        truckList.Add(truck);
                    }

                }
                else if (orderWeight > 2000000)
                {
                    for(int i = 1; i <= Math.Round(orderWeight/2000000); i ++)
                    {
                        truck = new TruckShipping(productList, truckWeight);
                        truckList.Add(truck);
                        productList.Add(productId);
                        truckWeight = orderWeight / 2000000;
                        productList = new List<int>();
                    }
                }
                else
                {
                    truck = new TruckShipping(productList, truckWeight);
                    truckList.Add(truck);
                    truckWeight = 0;
                    productList = new List<int>();
                }
            }
            return truckList;
        }
        private void CheckStockInWarehouse(int WarehouseId, List<StockAlteration> lineItems)
        {

            List<string> errors;
            var stock = stockRepository.GetStockByWarehouseAndProductIds(WarehouseId, lineItems.Select(x => x.ProductId).ToList());

            errors = new List<string>();

            for (int i = 0; i < lineItems.Count; i++)
            {
                var lineItem = lineItems[i];

                if (!stock.ContainsKey(lineItem.ProductId))
                {
                    errors.Add(string.Format("Product: {0}, no stock Held", lineItem.Gtin));
                    continue;
                }

                var item = stock[lineItem.ProductId];
                if (lineItem.Quantity > item.held)
                {
                    errors.Add(string.Format("Product: {0}, stock Held: {1}, stock to remove: {2}", lineItem.Gtin, item.held, lineItem.Quantity));
                }
            }

            if (errors.Count > 0)
            {
                throw new InsufficientStockException(string.Join("; ", errors));
            }
        }
        private static List<string> CreateProductNumbers(OutboundOrderRequestModel request)
        {

            var productNumbers = new List<String>();
            foreach (var orderLine in request.OrderLines)
            {
                if (productNumbers.Contains(orderLine.gtin))
                {
                    throw new ValidationException(String.Format("Outbound order request contains duplicate product gtin: {0}", orderLine.gtin));
                }

                productNumbers.Add(orderLine.gtin);
            }

            return productNumbers;
        }
    }
}