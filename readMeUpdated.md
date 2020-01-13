ShipIt Inventory Management
===========================

Copyright 2010.

# Setup Instructions

## Running the application

### With Visual Studio

To run the app via Visual Studio:

* Open the `ShipIt.sln` solution by going to `File` -> `Open` -> `Project/Solution`
* Add a connections.config to both the ShipIt and ShipItTest projects, adding a connection string to each e.g.

```
<connectionStrings>
  <add name="MyPostgres" providerName="System.Data.SqlClient" connectionString="Server=127.0.0.1;Port=5432;Database=ShipItTest;User Id=postgres; Password=password;" />
</connectionStrings>
```

* Copy Database Backup file into ShipIt directory
* Restore the database file in PostGres ie. create ShipItTest database, then restore the backup file into this database
* Ensure that the connections.config file only has the 3 lines printed above
* Ensure that the main and test connection strings point to different databases
* Ensure all tests return passed before moving forward

### On AWS Elastic Beanstalk

To update a running [AWS Elastic Beanstalk](https://aws.amazon.com/elasticbeanstalk/) instance:

* Install [AWS Toolkit for Visual Studio](https://aws.amazon.com/visualstudio/)
* Open the Warehouses-CSharp project in Visual Studio and add your AWS credentials to the AWS Toolkit
* Right click on the ShipIt project and select Publish to AWS
* Select the region your prod environment is running on and redeploy to that environment

To check the logs:  From the AWS console, go to `Services` -> `Elastic Beanstalk`, and
choose your instance from the dashboard.   Click `Logs` on the left, then `Request Logs`.

In the unlikely event that you need to change any of the injected configuration, for
example the database connection string or password, then these are available under
`Configuration` -> `Software`.

Information on the CPU utilisation, and network utilisation is available under `Monitoring`,
it may also be interesting to look at the utilisation or logs of the PostgreSQL database instance
which backs this application.  These are available under `Services` -> `RDS` -> `Databases`
-> `shipit`.

## Unit Tests

Run the tests in Visual Studio by right clicking on the `ShipItTest` project and
choosing "Run Tests".

Due to the "age" of the codebase, the unit tests rely on a connection to the database.
Therefore you will need to ensure that you have a database setup whose schema matches
the production database.  This database can be local or remote.  The details of this
database are set in the test project's connections.config file that you should have created earlier.
