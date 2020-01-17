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
        private readonly static Company COMPANY = new CompanyBuilder().CreateCompany();
        private static readonly int WAREHOUSE_ID = EMPLOYEE.WarehouseId;

        private Product product;
        private int productId;
        private const string GTIN = "0000";

        public new void OnSetUp()
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
            OnSetUp();
            stockRepository.AddStock(WAREHOUSE_ID, new List<StockAlteration>() { new StockAlteration(productId, 10, 0, product.Gtin) });
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
            OnSetUp();
            stockRepository.AddStock(WAREHOUSE_ID, new List<StockAlteration>() { new StockAlteration(productId, 10,0, product.Gtin) });
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
            OnSetUp();
            var noStockGtin = GTIN + "XYZ";
            productRepository.AddProducts(new List<ProductDataModel>() { new ProductBuilder().setGtin(noStockGtin).CreateProductDatabaseModel() });
            stockRepository.AddStock(WAREHOUSE_ID, new List<StockAlteration>() { new StockAlteration(productId, 10, 0, product.Gtin) });

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
            OnSetUp();
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
            OnSetUp();
            stockRepository.AddStock(WAREHOUSE_ID, new List<StockAlteration>() { new StockAlteration(productId, 10, 0, product.Gtin) });
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

        [TestMethod]
        public void TestTruckCantBeOverLoadedWithOneOrder()
        {
            //ARRANGE
            onSetUp();
            var productA = new ProductBuilder().setGtin("0001").setWeight(2000000.0f).CreateProductDatabaseModel();
            productRepository.AddProducts(new List<ProductDataModel>() { productA });
            var productAID = new Product(productRepository.GetProductByGtin("0001")).Id;
            stockRepository.AddStock(WAREHOUSE_ID, new List<StockAlteration>() { new StockAlteration(productAID, 10, productA.Weight, productA.Gtin) });

            var productId = productA.Id;
            var Gtin = productA.Gtin;
            var Weight = productA.Weight;
            var Quantity = 3;
            var orderItems = new List<StockAlteration>();

            orderItems.Add(new StockAlteration(productId, Quantity, Weight, Gtin));
            
            //Act
            var truckList = OutboundOrderController.TruckLoading(orderItems);

            //ASSERT
            //Expected Result = 3 trucks
            Assert.AreEqual(3, truckList.Count);
        }


        [TestMethod]
        public void TestNewTruckForEachOrder()
        {
            //ARRANGE
            onSetUp();
            var productA = new ProductBuilder().setGtin("0001").setWeight(2000000.0f).CreateProductDatabaseModel();
            productRepository.AddProducts(new List<ProductDataModel>() { productA });
            var productAID = new Product(productRepository.GetProductByGtin("0001")).Id;
            stockRepository.AddStock(WAREHOUSE_ID, new List<StockAlteration>() { new StockAlteration(productAID, 10, productA.Weight, productA.Gtin) });
            var productB = new ProductBuilder().setGtin("0002").setWeight(2000000.0f).CreateProductDatabaseModel();
            productRepository.AddProducts(new List<ProductDataModel>() { productB });
            var productBID = new Product(productRepository.GetProductByGtin("0002")).Id;
            stockRepository.AddStock(WAREHOUSE_ID, new List<StockAlteration>() { new StockAlteration(productBID, 10, productB.Weight, productB.Gtin) });

            var productId = productA.Id;
            var Gtin = productA.Gtin;
            var Weight = productA.Weight;
            var orderItems = new List<StockAlteration>
            {
                new StockAlteration(productA.Id, 1, productA.Weight, productA.Gtin),
                new StockAlteration(productB.Id, 1, productB.Weight, productB.Gtin)
            };

            //Act
            var truckList = OutboundOrderController.TruckLoading(orderItems);

            //ASSERT
            Assert.AreEqual(2, truckList.Count);
            Assert.AreEqual(1, truckList[0].ProductQuantityByIds.Count);
            Assert.AreEqual(1, truckList[0].ProductQuantityByIds[productA.Id]);
            Assert.AreEqual(1, truckList[1].ProductQuantityByIds.Count);
            Assert.AreEqual(1, truckList[1].ProductQuantityByIds[productB.Id]);
        }

        [TestMethod]
        public void TestMultipleOrdersToSameTruck()
        {
            //ARRANGE
            onSetUp();
            var productA = AddSampleProduct("0001", 350000.0f, 10);
            var productB = AddSampleProduct("0002", 350000.0f, 10);
            var productC = AddSampleProduct("0003", 350000.0f, 10);
            var productD = AddSampleProduct("0004", 350000.0f, 10);

            var productId = productA.Id;
            var Gtin = productA.Gtin;
            var Weight = productA.Weight;
            var orderItems = new List<StockAlteration>
            {
                new StockAlteration(productA.Id, 1, productA.Weight, productA.Gtin),
                new StockAlteration(productB.Id, 1, productB.Weight, productB.Gtin),
                new StockAlteration(productC.Id, 1, productC.Weight, productC.Gtin),
                new StockAlteration(productD.Id, 1, productD.Weight, productD.Gtin)
            };

            //Act
            var truckList = OutboundOrderController.TruckLoading(orderItems);

            //ASSERT
            Assert.AreEqual(1, truckList.Count);
            Assert.AreEqual(4, truckList[0].ProductQuantityByIds.Count);

            Assert.AreEqual(1, truckList[0].ProductQuantityByIds[productA.Id]);
            Assert.AreEqual(1, truckList[0].ProductQuantityByIds[productB.Id]);
            Assert.AreEqual(1, truckList[0].ProductQuantityByIds[productC.Id]);
            Assert.AreEqual(1, truckList[0].ProductQuantityByIds[productD.Id]);
        }

        [TestMethod]
        public void TestPartiallyFilledTruck()
        {
            //ARRANGE
            onSetUp();
            var productA = AddSampleProduct("0001", 350000.0f, 10);
            var productB = AddSampleProduct("0002", 350000.0f, 10);
            var productC = AddSampleProduct("0003", 350000.0f, 10);
            var productD = AddSampleProduct("0004", 350000.0f, 10);
            var productE = AddSampleProduct("0005", 601000.0f, 10);

            var productId = productA.Id;
            var Gtin = productA.Gtin;
            var Weight = productA.Weight;
            var orderItems = new List<StockAlteration>
            {
                new StockAlteration(productA.Id, 1, productA.Weight, productA.Gtin),
                new StockAlteration(productB.Id, 1, productB.Weight, productB.Gtin),
                new StockAlteration(productC.Id, 1, productC.Weight, productC.Gtin),
                new StockAlteration(productD.Id, 1, productD.Weight, productD.Gtin),
                new StockAlteration(productE.Id, 1, productE.Weight, productE.Gtin)
            };

            //Act
            var truckList = OutboundOrderController.TruckLoading(orderItems);

            //ASSERT
            Assert.AreEqual(2, truckList.Count);
        }

        [TestMethod]
        public void TestProductNotSplitUneccessarily()
        {
            //ARRANGE
            onSetUp();
            var productA = AddSampleProduct("0001", 1000.0f, 10);
            var productB = AddSampleProduct("0002", 1000.0f, 10);

            var productId = productA.Id;
            var Gtin = productA.Gtin;
            var Weight = productA.Weight;
            var orderItems = new List<StockAlteration>
            {
                new StockAlteration(productA.Id, 1000, productA.Weight, productA.Gtin),
                new StockAlteration(productB.Id, 1999, productB.Weight, productB.Gtin)
            };

            //Act
            var truckList = OutboundOrderController.TruckLoading(orderItems);

            //ASSERT
            Assert.AreEqual(2, truckList.Count);
            Assert.AreEqual(1, truckList[0].ProductQuantityByIds.Count);
            Assert.AreEqual(1, truckList[1].ProductQuantityByIds.Count);
        }

        private Product AddSampleProduct(string gtin, float weight, int quantity)
        {

            var productToCreate=new ProductBuilder().setGtin(gtin).setWeight(weight).CreateProductDatabaseModel();
            productRepository.AddProducts(new List<ProductDataModel>()
            {
                productToCreate
            });
            var product =new Product(productRepository.GetProductByGtin(gtin));
            stockRepository.AddStock(WAREHOUSE_ID,new List<StockAlteration>()
            {
                new StockAlteration(product.Id,quantity,product.Weight,product.Gtin)
            });
            return product;
        }
    }
}