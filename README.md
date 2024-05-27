# Data Calculations Documentation

This documentation provides instructions on how to perform data calculations on the cloud using Azure Stream Analytics Jobs and store the results in the IoT platform.

## Prerequisites

Ensure you have the following prerequisites in place:

- An Azure account
- Azure IoT Hub set up
- Azure Stream Analytics Job set up
- Blob storage for storing analytics data

## Setting Up the Project

### 1. Clone the Project

```shell
git clone https://github.com/VrMonterrey/IoTHub
```

### 2. Install the C# SDK

Download and install the C# SDK from [here](https://dotnet.microsoft.com/en-us/download).

### 3. Configure the Agent

Create an `appSettings.json` file in the folder containing your `Program.cs` file with the following content:

```json
{
  "OPC_CONNECTION_STRING": "opc.tcp://localhost:4840/", // standard for local development
  "AZURE_IOT_CONNECTION_STRING": "HostName=%azure-iot-host-name%;",

  "DEVICES": [
    {
      "deviceName": "device-name",
      "opcNodeId": "%opc-node-id%",
      "azureDeviceId": "DeviceId=%device-name;SharedAccessKey=%device-shared-key%"
    }
    //...
  ]
}
```

- `OPC_CONNECTION_STRING`: Connection string for the OPC server.
- `AZURE_IOT_CONNECTION_STRING`: Base prefix string for all device connection strings for Azure IoT Hub.
- `DEVICES`: List of devices with the following properties:
  - `deviceName`: Identifier for the device (unique and not from OPC or Azure IoT Hub).
  - `opcNodeId`: OPC Node ID string used to connect to the OPC device.
  - `azureDeviceId`: Partial connection string to Azure IoT Hub Device. Combined with `AZURE_IOT_CONNECTION_STRING`, it creates a full connection string to the IoT Hub device.

### 4. Run the Application

Run the application using the following command:

```shell
dotnet run .
```

> Note: Run this command inside the `IoT_Case_Study_OC.IoT_Case_Study_OC` directory.

## Performing Data Calculations

Data calculations are performed using Azure Stream Analytics Jobs. Below are the steps and queries for each type of calculation:

### 1. Production KPIs

**Objective**: Calculate the percentage of good production in total volume, grouped by device in 5-minute windows.

### Query

```sql
SELECT
    IoTHub.ConnectionDeviceId AS DeviceId,
    System.Timestamp AS WindowEnd,
    SUM(good_count) * 100.0 / (SUM(good_count) + SUM(bad_count)) AS GoodProductionPercentage
INTO
    [ProductionKPI]
FROM
    [VrMonterrey]
GROUP BY
    IoTHub.ConnectionDeviceId,
    TumblingWindow(minute, 5)
```

### Explanation

- **DeviceId**: Identifier for the device sending data.
- **WindowEnd**: Timestamp indicating the end of the 5-minute window.
- **GoodProductionPercentage**: The percentage of good production calculated as:
  \[
  \text{GoodProductionPercentage} = \frac{\text{SUM(good_count)} \times 100.0}{\text{SUM(good_count)} + \text{SUM(bad_count)}}
  \]

- **Group By**: Data is grouped by the device ID and a tumbling window of 5 minutes.

### 2. Temperature

**Objective**: Every minute, calculate the average, minimum, and maximum temperature over the last 5 minutes, grouped by device.

### Query

```sql
SELECT
    IoTHub.ConnectionDeviceId AS DeviceId,
    System.Timestamp AS WindowEnd,
    AVG(temperature) AS AvgTemperature,
    MIN(temperature) AS MinTemperature,
    MAX(temperature) AS MaxTemperature
INTO
    [Temperature]
FROM
    [VrMonterrey]
GROUP BY
    IoTHub.ConnectionDeviceId,
    TumblingWindow(minute, 1)
```

### Explanation

- **DeviceId**: Identifier for the device sending data.
- **WindowEnd**: Timestamp indicating the end of the 1-minute window.
- **AvgTemperature**: The average temperature over the last 5 minutes.
- **MinTemperature**: The minimum temperature recorded over the last 5 minutes.
- **MaxTemperature**: The maximum temperature recorded over the last 5 minutes.

- **Group By**: Data is grouped by the device ID and a tumbling window of 1 minute.

### 3. Device Errors

**Objective**: Detect situations whenever a device experiences more than 3 errors in under 1 minute.

### Query

```sql
SELECT
    IoTHub.ConnectionDeviceId AS DeviceId,
    System.Timestamp AS WindowEnd,
    COUNT(*) AS ErrorCount
INTO
    [DeviceErrors]
FROM
    [VrMonterrey]
WHERE
    bad_count > 0
GROUP BY
    IoTHub.ConnectionDeviceId,
    TumblingWindow(minute, 1)
HAVING
    COUNT(*) > 3
```

### Explanation

- **DeviceId**: Identifier for the device sending data.
- **WindowEnd**: Timestamp indicating the end of the 1-minute window.
- **ErrorCount**: The number of errors detected in the 1-minute window.
- **Filter**: Only include records where `bad_count` is greater than 0.
- **Group By**: Data is grouped by the device ID and a tumbling window of 1 minute.
- **Having Clause**: Ensures that only windows with more than 3 errors are selected.

## Storage

The analytics data produced by these queries is stored in Azure Blob Storage.

# Business Logic Implementation Documentation

This documentation explains how to implement the business logic for handling device errors and production rate monitoring on the cloud using Azure Event Hubs and Azure Functions.

## Prerequisites

Ensure you have the following prerequisites in place:

- An Azure account
- Azure IoT Hub set up
- Azure Event Hubs set up
- Azure Functions set up
- Blob storage for storing analytics data

The following business logic must be implemented on the cloud (IoT platform):

### 1. Device Errors

If a device experiences more than 3 errors in under 1 minute:

- **Trigger Emergency Stop**: Immediately trigger Emergency Stop on the device.

### Query

```sql
SELECT
    IoTHub.ConnectionDeviceId AS DeviceId,
    System.Timestamp AS WindowEnd,
    COUNT(*) AS ErrorCount
INTO
    [DeviceErrors]
FROM
    [VrMonterrey]
WHERE
    bad_count > 0
GROUP BY
    IoTHub.ConnectionDeviceId,
    TumblingWindow(minute, 1)
HAVING
    COUNT(*) > 3
```

### Implementation with Azure Functions

1. Set up an Azure Function to listen to the Event Hub where `DeviceErrors` data is streamed.
2. In the function, check if the `ErrorCount` exceeds 3.
3. If the condition is met, use the Azure IoT SDK to invoke the `emergencyStop` direct method on the device.

### 2. Production Rate Monitoring

If a device experiences a drop in good production rate below 90%:

- **Decrease Desired Production Rate**: Decrease the `ProductionRate` by 10 points.

### Query

```sql
SELECT
    IoTHub.ConnectionDeviceId AS DeviceId,
    System.Timestamp AS WindowEnd,
    SUM(good_count) * 100.0 / (SUM(good_count) + SUM(bad_count)) AS GoodProductionPercentage
INTO
    [ProductionKPI]
FROM
    [VrMonterrey]
GROUP BY
    IoTHub.ConnectionDeviceId,
    TumblingWindow(minute, 5)
```

### Implementation with Azure Functions

1. Set up an Azure Function to listen to the Event Hub where `ProductionKPI` data is streamed.
2. In the function, check if `GoodProductionPercentage` falls below 90%.
3. If the condition is met, update the `Desired` properties in the Device Twin to decrease the `ProductionRate` by 10 points.

### 3. General Device Errors

If a device error occurs (of any type):

- **Send an Email**: Send an email to a predefined address.

### Implementation with Azure Functions

1. Set up an Azure Function to listen to the Event Hub where device telemetry data is streamed.
2. In the function, check for any `DeviceError` events.
3. If an error is detected, use an email service (such as SendGrid) to send an email notification.

## Data Flow Description

The following text illustrates the data flow and business logic implementation:

1. **Error Handling**:
    - Device experiences more than 3 errors in under 1 minute.
    - Trigger Emergency Stop on the device.

2. **Production Rate Monitoring**:
    - Good production rate drops below 90%.
    - Decrease Desired Production Rate by 10 points.

3. **General Device Errors**:
    - Device error occurs.
    - Send an email notification.
