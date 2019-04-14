# Unit Test Helper Library

[![Build Status](https://travis-ci.org/fdecaire/UnitTestHelperLibrary.svg?branch=master)](https://travis-ci.org/fdecaire/UnitTestHelperLibrary)
[![NuGet](https://img.shields.io/nuget/v/Nuget.Core.svg?maxAge=2592000)]()

NuGet Package:
https://www.nuget.org/packages/UnitTestHelperLibrary/

Install-Package UnitTestHelperLibrary -Version 1.1.2

# How to Use this Package

This unit testing package can be used to create unit tests based in an instance of a SQLLocalDB database.  You must install the SQLLocalDB product when you install MS SQL Server on the machine that will run these unit tests.  There are methods available to create databases and tables inside the in-memory SQLLocalDB instance.  Once your unit tests complete, there is a tear-down method to clean it all up and remove the database instance.  To seed your data for testing, you can select data from your database and convert into xml format.  This data can be pasted into an embedded C# file in your unit test project to be inserted before a unit test is executed.

# Setting up the Unit Test Project

The instructions here are for MS Test projects only at this time.  After you create an MS Test unit test project in your solution, you must create a cs file to contain your startup and shutdown code (known as Assembly Initialize and Assembly Cleanup).  Be sure and add the UnitTestHelperLibrary NuGet package to this project.  Here is an example of what the code will look like:

```C#
[TestClass]
public class AssemblyUnitTestShared
{
	[AssemblyInitialize]
	public static void ClassStartInitialize(TestContext testContext)
	{
		UnitTestHelpers.Start("sampledatatestinstance", new string[] { "Linq2SqlDemoData" });

		// create tables
		UnitTestHelpers.CreateAllTables(Linq2SqlDemoDataTables.TableList, Linq2SqlDemoDataTables.DatabaseName);
	}

	[AssemblyCleanup]
	public static void ClassEndCleanup()
	{
		UnitTestHelpers.End();
	}
}
```

In the UnitTestHelpers.Start method the first parameter is a name you can use for your instance.  This will be appended with a GUID so it will end up being a unique name.  The only purpose this serves is so you can identify the file that is created on your hard drive if you want to manually cleanup failed runs (like on a build server).  The files are normally created in the "%userprofile%\AppData\Local\Microsoft\Microsoft SQL Server Local DB\Instances" directory.

The second parameter in the UnitTestHelpers.Start method is an array of strings that represents all the databases you would like to create in your instance.  In the example above, there is only one database named Linq2SqlDemoData created in the instance.

The UnitTestHelpers.CreateAllTables method can be used to create all tables in your database.  You can also provide a list of tables to provide manually.  In order to preform this step, you'll need to generate the C# code that you'll use to create tables, stored procedures, views, etc.  For that, you'll need to download the database generate application that is normally used with this unit testing package (download it from here https://github.com/fdecaire/UnitTestDatabaseGenerator).

Run the generator against your database and a collection of cs files will be created.  You can dump that directory into your test directory and use the code directly from there.  This code does not get deployed.

Inside each unit test class you create in your project, you'll need to do a cleanup.  Add this method:

```C#
[TestCleanup]
public void Cleanup()
{
	UnitTestHelpers.TruncateData();
}
```

This will cause all of your data to be cleaned out of your database before the next test is run.

# Using XML Data to Seed Your Unit Test

If you want to seed your data automatically, you can dump some data into an xml file that this utility can read and insert all at once.  You can seed all of your tables at one time and save a lot of unit testing code.  First, you have to get an xml copy of some data.  You can create a query against your test database and have SQL Server spit out the xml that you'll need.  After this query, you can manually modify the xml data as desired to setup edge-case tests.  First, here is a sample query that you can use on MS SQL:

```SQL
select * from employee for xml raw ('employee')
```

This will select all records in a table called employee.  That data will go into an xml block like this:

```xml
<employee ID="1" FirstName="Joe" LastName="Cool" Department="1" />
<employee ID="2" FirstName="Jane" LastName="Whipple" Department="2" />
<employee ID="3" FirstName="Mark" LastName="Delaney" Department="2" />
<employee ID="4" FirstName="Sue" LastName="Smith" Department="3" />
```

In order to use this with your unit tests, you'll need to create an xml file in your unit test project.  I usually create a TestData directory to contain all of these files.  Once you create an empty xml file, you will need to change the Build Action in the file properties in Visual Studio to "Embedded".  This will cause the xml file to be compiled into the help project dll.  This feature will save a lot of time when setting up a build server, since there are no extra file paths to worry about.

The above snippet of xml is not valid for use in an xml file yet.  You must first setup the file to look like this:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<databases>
  <database name="Linq2SqlDemoData">

  </database>
</databases>
```

You can include multiple databases in your xml file if you want to seed more than one database.  Inside the database tag, you paste rows of table data that will be inserted into that database.  That's where you'll paste the output obtained from MS SQL server to form an xml file like this:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<databases>
  <database name="Linq2SqlDemoData">
    <employee ID="1" FirstName="Joe" LastName="Cool" Department="1" />
    <employee ID="2" FirstName="Jane" LastName="Whipple" Department="2" />
    <employee ID="3" FirstName="Mark" LastName="Delaney" Department="2" />
    <employee ID="4" FirstName="Sue" LastName="Smith" Department="3" />
  </database>
</databases>
```

