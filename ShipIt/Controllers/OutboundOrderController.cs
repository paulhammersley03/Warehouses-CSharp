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
            double truckWeight = 0;
            var weightLimit = 2000000;
            var counter = 0; 
            Dictionary<int, int> productList = new Dictionary<int, int>();
            List<TruckShipping> truckList = new List<TruckShipping>();
            TruckShipping truck = new TruckShipping(productList, truckWeight);

            foreach (var order in orderItems)
            {
                counter = counter + 1;
                double orderWeight = order.Weight;
                double remainder = orderWeight;
                
                if (order.Weight >= weightLimit)
                {
                    while (remainder > 0)
                    {
                        productList.Add(order.ProductId, order.Quantity);
                        truckWeight = weightLimit;
                        truck = AddTruck(truckList, truck, weightLimit, ref productList, ref truckWeight, ref remainder);
                    }
                }
                else if (orderWeight + truckWeight <= weightLimit)
                {
                    productList.Add(order.ProductId, order.Quantity);
                    truckWeight = truckWeight + order.Weight;
                    if (counter == orderItems.Count() && truckList.Count() == 0)
                    {
                        truck = AddTruck(truckList, truck, weightLimit, ref productList, ref truckWeight, ref remainder);
                    }

                }
                else if (orderWeight + truckWeight > weightLimit)
                {
                    truck = AddTruck(truckList, truck, weightLimit, ref productList, ref truckWeight, ref remainder);
                    productList.Add(order.ProductId, order.Quantity);
                    truckWeight = truckWeight + order.Weight;
                    truck = AddTruck(truckList, truck, weightLimit, ref productList, ref truckWeight, ref remainder);
                }
                

                //if (counter == orderItems.Count() && truckList.Count() == 0)
                //{
                //    truck = AddTruck(truckList, truck, weightLimit, ref productList, ref truckWeight, ref remainder);
                //}

            }
            return truckList;
        }

        private static TruckShipping AddTruck(List<TruckShipping> truckList,TruckShipping truck,int weightLimit,ref Dictionary<int, int> productList,ref double truckWeight,ref double remainder)
        {

            double orderWeight;
            truckList.Add(truck);
            truck=new TruckShipping(productList,truckWeight);
            productList=new Dictionary<int, int>();
            truckWeight=0;
            remainder=remainder-weightLimit;
            orderWeight=remainder;
            return truck;
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