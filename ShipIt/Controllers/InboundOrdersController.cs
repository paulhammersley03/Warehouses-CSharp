using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using System.Web.Mvc;
using ShipIt.Exceptions;
using ShipIt.Models.ApiModels;
using ShipIt.Models.DataModels;
using ShipIt.Parsers;
using ShipIt.Repositories;
using ShipIt.Validators;

namespace ShipIt.Controllers
{
    public class InboundOrderController : ApiController
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private readonly IEmployeeRepository employeeRepository;
        private readonly ICompanyRepository companyRepository;
        private readonly IProductRepository productRepository;
        private readonly IStockRepository stockRepository;

        public InboundOrderController(IEmployeeRepository employeeRepository, ICompanyRepository companyRepository, IProductRepository productRepository, IStockRepository stockRepository)
        {
            this.employeeRepository = employeeRepository;
            this.stockRepository = stockRepository;
            this.companyRepository = companyRepository;
            this.productRepository = productRepository;
        }

        // GET api/status/{warehouseId}
        public InboundOrderResponse Get(int warehouseId)
        {
            log.Info("orderIn for warehouseId: " + warehouseId);
            var operationsManager = new Employee(employeeRepository.GetOperationsManager(warehouseId));

            log.Debug(String.Format("Found operations manager: {0}", operationsManager));
            var warehouseHeldItems = stockRepository.GetStockByWarehouseId(warehouseId);//gets warehouseID

           
            Dictionary<Company, List<InboundOrderLine>> orderlinesByCompany = new Dictionary<Company, List<InboundOrderLine>>();
            foreach (var item in warehouseHeldItems)
            {
                var warehouseStock = new WarehouseStock(item);
                
                if (warehouseStock.Held < warehouseStock.LowerThresh && !warehouseStock.Discontinued)
                {
                    var orderQuantity = Math.Max(item.lowerThresh * 3 - item.held, warehouseStock.MinimumOrderQuantity);

                    if (!orderlinesByCompany.ContainsKey(warehouseStock.Company))
                    {
                        orderlinesByCompany.Add(warehouseStock.Company, new List<InboundOrderLine>());
                    }

                    orderlinesByCompany[warehouseStock.Company].Add(
                        new InboundOrderLine()
                        {
                            gtin = warehouseStock.CompanyId,
                            name = warehouseStock.ProductName,
                            quantity = orderQuantity
                        });
                }
            }

            log.Debug(String.Format("Constructed order lines: {0}", orderlinesByCompany));

            var orderSegments = orderlinesByCompany.Select(ol => new OrderSegment()
            {
                OrderLines = ol.Value,
                Company = ol.Key
            });

            log.Info("Constructed inbound order");

            return new InboundOrderResponse()
            {
                OperationsManager = operationsManager,
                WarehouseId = warehouseId,
                OrderSegments = orderSegments.ToList()
            };
        }

        public void Post([FromBody]InboundManifestRequestModel requestModel)
        {
            log.Info("Processing manifest: " + requestModel);

            var gtins = new List<string>();

            foreach (var orderLine in requestModel.OrderLines)
            {
                if (gtins.Contains(orderLine.gtin))
                {
                    throw new ValidationException(String.Format("Manifest contains duplicate product gtin: {0}", orderLine.gtin));
                }
                gtins.Add(orderLine.gtin);
            }

            IEnumerable<ProductDataModel> productDataModels = productRepository.GetProductsByGtin(gtins);
            Dictionary<string, Product> products = productDataModels.ToDictionary(p => p.Gtin, p => new Product(p));

            log.Debug(String.Format("Retrieved products to verify manifest: {0}", products));

            var lineItems = new List<StockAlteration>();
            var errors = new List<string>();

            foreach (var orderLine in requestModel.OrderLines)
            {
                if (!products.ContainsKey(orderLine.gtin))
                {
                    errors.Add(String.Format("Unknown product gtin: {0}", orderLine.gtin));
                    continue;
                }

                Product product = products[orderLine.gtin];
                if (!product.Gcp.Equals(requestModel.Gcp))
                {
                    errors.Add(String.Format("Manifest GCP ({0}) doesn't match Product GCP ({1})",
                        requestModel.Gcp, product.Gcp));
                }
                else
                {
                    lineItems.Add(new StockAlteration(product.Id, orderLine.quantity));
                }
            }

            if (errors.Count() > 0)
            {
                log.Debug(String.Format("Found errors with inbound manifest: {0}", errors));
                throw new ValidationException(String.Format("Found inconsistencies in the inbound manifest: {0}", String.Join("; ", errors)));
            }

            log.Debug(String.Format("Increasing stock levels with manifest: {0}", requestModel));
            stockRepository.AddStock(requestModel.WarehouseId, lineItems);
            log.Info("Stock levels increased");
        }
    }
}
