using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ShipIt.Controllers;
using ShipIt.Exceptions;
using ShipIt.Models.ApiModels;
using ShipIt.Models.DataModels;
using ShipIt.Repositories;
using ShipItTest.Builders;

namespace ShipItTest
{
    [TestClass]
    public class InboundOrderControllerTests : AbstractBaseTest
    {
        InboundOrderController inboundOrderController = new InboundOrderController(
            new EmployeeRepository(),
            new CompanyRepository(),
            new ProductRepository(),
            new StockRepository()
        );
        StockRepository stockRepository = new StockRepository();
        CompanyRepository companyRepository = new CompanyRepository();
        ProductRepository productRepository = new ProductRepository();
        EmployeeRepository employeeRepository = new EmployeeRepository();

        private static Employee OPS_MANAGER = new EmployeeBuilder().CreateEmployee();
        private static Company COMPANY = new CompanyBuilder().CreateCompany();
        private static readonly int WAREHOUSE_ID = OPS_MANAGER.WarehouseId;
        private static readonly String GCP = COMPANY.Gcp;

        private Product product;
        private int productId;
        private const string GTIN = "0000";

        public new void OnSetUp()
        {
            base.onSetUp();
            employeeRepository.AddEmployees(new List<Employee>() { OPS_MANAGER });
            companyRepository.AddCompanies(new List<Company>() { COMPANY });
            var productDataModel = new ProductBuilder().setGtin(GTIN).CreateProductDatabaseModel();
            productRepository.AddProducts(new List<ProductDataModel>() { productDataModel });
            product = new Product(productRepository.GetProductByGtin(GTIN));
            productId = product.Id;
        }

        [TestMethod]
        public void TestCreateOrderNoProductsHeld()
        {
            OnSetUp();

            var inboundOrder = inboundOrderController.Get(WAREHOUSE_ID);

            Assert.AreEqual(inboundOrder.WarehouseId, WAREHOUSE_ID);
            Assert.IsTrue(EmployeesAreEqual(inboundOrder.OperationsManager, OPS_MANAGER));
            Assert.AreEqual(inboundOrder.OrderSegments.Count(), 0);
        }

        [TestMethod]
        public void TestCreateOrderProductHoldingNoStock()
        {
            OnSetUp();
            stockRepository.AddStock(WAREHOUSE_ID, new List<StockAlteration>() { new StockAlteration(productId, 0, 0, product.Gtin) });

            var inboundOrder = inboundOrderController.Get(WAREHOUSE_ID);

            Assert.AreEqual(inboundOrder.OrderSegments.Count(), 1);
            var orderSegment = inboundOrder.OrderSegments.First();
            Assert.AreEqual(orderSegment.Company.Gcp, GCP);
        }

        [TestMethod]
        public void TestCreateOrderProductHoldingSufficientStock()
        {
            OnSetUp();
            stockRepository.AddStock(WAREHOUSE_ID, new List<StockAlteration>() { new StockAlteration(productId, product.LowerThreshold, 0, product.Gtin) });

            var inboundOrder = inboundOrderController.Get(WAREHOUSE_ID);

            Assert.AreEqual(inboundOrder.OrderSegments.Count(), 0);
        }

        [TestMethod]
        public void TestCreateOrderDiscontinuedProduct()
        {
            OnSetUp();
            stockRepository.AddStock(WAREHOUSE_ID, new List<StockAlteration>() { new StockAlteration(productId, product.LowerThreshold - 1, 0, product.Gtin) });
            productRepository.DiscontinueProductByGtin(GTIN);

            var inboundOrder = inboundOrderController.Get(WAREHOUSE_ID);

            Assert.AreEqual(inboundOrder.OrderSegments.Count(), 0);
        }

        [TestMethod]
        public void TestProcessManifest()
        {
            OnSetUp();
            var quantity = 12;
            var inboundManifest = new InboundManifestRequestModel()
            {
                WarehouseId = WAREHOUSE_ID,
                Gcp = GCP,
                OrderLines = new List<OrderLine>()
                {
                    new OrderLine()
                    {
                        gtin = GTIN,
                        quantity = quantity
                    }
                }
            };

            inboundOrderController.Post(inboundManifest);

            var stock = stockRepository.GetStockByWarehouseAndProductIds(WAREHOUSE_ID, new List<int>() {productId})[productId];
            Assert.AreEqual(stock.held, quantity);
        }

        [TestMethod]
        public void TestProcessManifestRejectsDodgyGcp()
        {
            OnSetUp();
            var quantity = 12;
            var dodgyGcp = GCP + "XYZ";
            var inboundManifest = new InboundManifestRequestModel()
            {
                WarehouseId = WAREHOUSE_ID,
                Gcp = dodgyGcp,
                OrderLines = new List<OrderLine>()
                {
                    new OrderLine()
                    {
                        gtin = GTIN,
                        quantity = quantity
                    }
                }
            };

            try
            {
                inboundOrderController.Post(inboundManifest);
                Assert.Fail("Expected exception to be thrown.");
            }
            catch (ValidationException e)
            {
                Assert.IsTrue(e.Message.Contains(dodgyGcp));
            }
        }

        [TestMethod]
        public void TestProcessManifestRejectsUnknownProduct()
        {
            OnSetUp();
            var quantity = 12;
            var unknownGtin = GTIN + "XYZ";
            var inboundManifest = new InboundManifestRequestModel()
            {
                WarehouseId = WAREHOUSE_ID,
                Gcp = GCP,
                OrderLines = new List<OrderLine>()
                {
                    new OrderLine()
                    {
                        gtin = GTIN,
                        quantity = quantity
                    },
                    new OrderLine()
                    {
                        gtin = unknownGtin,
                        quantity = quantity
                    }
                }
            };

            try
            {
                inboundOrderController.Post(inboundManifest);
                Assert.Fail("Expected exception to be thrown.");
            }
            catch (ValidationException e)
            {
                Assert.IsTrue(e.Message.Contains(unknownGtin));
            }
        }

        [TestMethod]
        public void TestProcessManifestRejectsDuplicateGtins()
        {
            OnSetUp();
            var quantity = 12;
            var inboundManifest = new InboundManifestRequestModel()
            {
                WarehouseId = WAREHOUSE_ID,
                Gcp = GCP,
                OrderLines = new List<OrderLine>()
                {
                    new OrderLine()
                    {
                        gtin = GTIN,
                        quantity = quantity
                    },
                    new OrderLine()
                    {
                        gtin = GTIN,
                        quantity = quantity
                    }
                }
            };

            try
            {
                inboundOrderController.Post(inboundManifest);
                Assert.Fail("Expected exception to be thrown.");
            }
            catch (ValidationException e)
            {
                Assert.IsTrue(e.Message.Contains(GTIN));
            }
        }

        private bool EmployeesAreEqual(Employee A, Employee B)
        {
            return A.WarehouseId == B.WarehouseId
                   && A.Name == B.Name
                   && A.role == B.role
                   && A.ext == B.ext;
        }
    }
}
