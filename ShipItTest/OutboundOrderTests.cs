using System;
using System.Collections.Generic;
using System.Linq;
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
    public class OutboundOrderControllerTests : AbstractBaseTest
    {
        OutboundOrderController outboundOrderController = new OutboundOrderController(
            new StockRepository(),
            new ProductRepository()
        );
        StockRepository stockRepository = new StockRepository();
        CompanyRepository companyRepository = new CompanyRepository();
        ProductRepository productRepository = new ProductRepository();
        EmployeeRepository employeeRepository = new EmployeeRepository();

        private static Employee EMPLOYEE = new EmployeeBuilder().CreateEmployee();
        private static Company COMPANY = new CompanyBuilder().CreateCompany();
        private static readonly int WAREHOUSE_ID = EMPLOYEE.WarehouseId;

        private Product product;
        private int productId;
        private const string GTIN = "0000";

        public new void onSetUp()
        {
            base.onSetUp();
            employeeRepository.AddEmployees(new List<Employee>() { EMPLOYEE });
            companyRepository.AddCompanies(new List<Company>() { COMPANY });
            var productDataModel = new ProductBuilder().setGtin(GTIN).CreateProductDatabaseModel();
            productRepository.AddProducts(new List<ProductDataModel>() { productDataModel });
            product = new Product(productRepository.GetProductByGtin(GTIN));
            productId = product.Id;
        }

        [TestMethod]
        public void TestOutboundOrder()
        {
            onSetUp();
            stockRepository.AddStock(WAREHOUSE_ID, new List<StockAlteration>() { new StockAlteration(productId, 10) });
            var outboundOrder = new OutboundOrderRequestModel()
            {
                WarehouseId = WAREHOUSE_ID,
                OrderLines = new List<OrderLine>()
                {
                    new OrderLine()
                    {
                        gtin = GTIN,
                        quantity = 3
                    }
                }
            };

            outboundOrderController.Post(outboundOrder);

            var stock = stockRepository.GetStockByWarehouseAndProductIds(WAREHOUSE_ID, new List<int>() { productId })[productId];
            Assert.AreEqual(stock.held, 7);
        }

        [TestMethod]
        public void TestOutboundOrderInsufficientStock()
        {
            onSetUp();
            stockRepository.AddStock(WAREHOUSE_ID, new List<StockAlteration>() { new StockAlteration(productId, 10) });
            var outboundOrder = new OutboundOrderRequestModel()
            {
                WarehouseId = WAREHOUSE_ID,
                OrderLines = new List<OrderLine>()
                {
                    new OrderLine()
                    {
                        gtin = GTIN,
                        quantity = 11
                    }
                }
            };

            try
            {
                outboundOrderController.Post(outboundOrder);
                Assert.Fail("Expected exception to be thrown.");
            }
            catch (InsufficientStockException e)
            {
                Assert.IsTrue(e.Message.Contains(GTIN));
            }
        }

        [TestMethod]
        public void TestOutboundOrderStockNotHeld()
        {
            onSetUp();
            var noStockGtin = GTIN + "XYZ";
            productRepository.AddProducts(new List<ProductDataModel>() { new ProductBuilder().setGtin(noStockGtin).CreateProductDatabaseModel() });
            stockRepository.AddStock(WAREHOUSE_ID, new List<StockAlteration>() { new StockAlteration(productId, 10) });

            var outboundOrder = new OutboundOrderRequestModel()
            {
                WarehouseId = WAREHOUSE_ID,
                OrderLines = new List<OrderLine>()
                {
                    new OrderLine()
                    {
                        gtin = GTIN,
                        quantity = 8
                    },
                    new OrderLine()
                    {
                        gtin = noStockGtin,
                        quantity = 1000
                    }
                }
            };

            try
            {
                outboundOrderController.Post(outboundOrder);
                Assert.Fail("Expected exception to be thrown.");
            }
            catch (InsufficientStockException e)
            {
                Assert.IsTrue(e.Message.Contains(noStockGtin));
                Assert.IsTrue(e.Message.Contains("no stock Held"));
            }
        }

        [TestMethod]
        public void TestOutboundOrderBadGtin()
        {
            onSetUp();
            var badGtin = GTIN + "XYZ";

            var outboundOrder = new OutboundOrderRequestModel()
            {
                WarehouseId = WAREHOUSE_ID,
                OrderLines = new List<OrderLine>()
                {
                    new OrderLine()
                    {
                        gtin = GTIN,
                        quantity = 1
                    },
                    new OrderLine()
                    {
                        gtin = badGtin,
                        quantity = 1
                    }
                }
            };

            try
            {
                outboundOrderController.Post(outboundOrder);
                Assert.Fail("Expected exception to be thrown.");
            }
            catch (NoSuchEntityException e)
            {
                Assert.IsTrue(e.Message.Contains(badGtin));
            }
        }

        [TestMethod]
        public void TestOutboundOrderDuplicateGtins()
        {
            onSetUp();
            stockRepository.AddStock(WAREHOUSE_ID, new List<StockAlteration>() { new StockAlteration(productId, 10) });
            var outboundOrder = new OutboundOrderRequestModel()
            {
                WarehouseId = WAREHOUSE_ID,
                OrderLines = new List<OrderLine>()
                {
                    new OrderLine()
                    {
                        gtin = GTIN,
                        quantity = 1
                    },
                    new OrderLine()
                    {
                        gtin = GTIN,
                        quantity = 1
                    }
                }
            };

            try
            {
                outboundOrderController.Post(outboundOrder);
                Assert.Fail("Expected exception to be thrown.");
            }
            catch (ValidationException e)
            {
                Assert.IsTrue(e.Message.Contains(GTIN));
            }
        }
    }
}
