---
updatedAt: 2026-10-20T13:22:08.000Z
agentTools:
  projectIndex: https://developer.easee.com/llms.txt
---

# SignalR Streaming API (Home)

## Overview

Easee SignalR Streaming provides a real-time connection for receiving data from Easee products.

Applications can establish a connection to the SignalR products hub, subscribe to one or more products using their serial numbers, and receive updates as they occur.

SignalR is intended for **receiving real-time product data**. Commands and product control should be performed through the appropriate REST API rather than through the streaming connection.

***

## How SignalR Streaming Works

A typical integration consists of three steps:

1. **Connect** to the SignalR products hub using an authentication token.
2. **Subscribe** to the required products using their serial numbers.
3. **Receive and process** real-time updates from the subscribed products.

The SignalR connection provides a persistent channel through which updates can be delivered to the subscribing application.

***

# SignalR Hub

The products SignalR hub is:

```text
https://streams.easee.com/hubs/products
```

<br />

***

# Authentication

An access token is required when establishing the SignalR connection&#x20;

&#x20;obtained via this endpoint [https://developer.easee.com/reference/account\_authenticate](https://developer.easee.com/reference/account_authenticate "https://developer.easee.com/reference/account_authenticate")

The token is supplied when creating the SignalR connection and is used to authenticate the client with the hub.

The authentication mechanism is independent of the technology or programming language used by the client. Any client capable of establishing the required SignalR connection can use the streaming service, provided it supplies a valid authentication token.

The example implementation at the end of this documentation demonstrates one way of supplying an access token.

***

# Product Subscriptions

Products are identified using their **serial number**.

A client can subscribe to one or more products.

For example:

```text
EX2B54KY
EXMR5T4V
```

The client maintains a list of product serial numbers and subscribes to each product after establishing the SignalR connection.

The subscription mechanism used by the example is:

```text
SubscribeWithCurrentState
```

with the product serial number and the current-state option supplied as parameters.

***

## Subscribing to Multiple Products

Multiple products can be subscribed to using their individual serial numbers.

Conceptually:

```text
Connect
   │
   ▼
Subscribe → Product A
   │
   ▼
Subscribe → Product B
   │
   ▼
Subscribe → Product C
   │
   ▼
Receive updates
```

This allows a single SignalR client to listen for updates from multiple subscribed products.

***

# Receiving Product Updates

Once a product has been subscribed to, the client can listen for `ProductUpdate` messages.

The client registers an event handler for the `ProductUpdate` event and processes the received message.

```text
Product
   │
   │ Update
   ▼
SignalR Hub
   │
   │ ProductUpdate
   ▼
Client Event Handler
   │
   ▼
Application Processing
```

The event handler provides the integration point for applications to process, store or otherwise act on received product data.

***

# Message Format

A product update is represented by the following message structure:

```text
mid: string
dataType: number
id: number
timestamp: string
value: any
```

<br />

## Fields

| Field       | Type     | Description                                                                                                     |
| ----------- | -------- | --------------------------------------------------------------------------------------------------------------- |
| `mid`       | `string` | Device ID                                                                                                       |
| `dataType`  | `number` | Type of data, for example string or integer                                                                     |
| `id`        | `number` | Observation ID                                                                                                  |
| `timestamp` | `string` | Timestamp of the data                                                                                           |
| `value`     | `string` | The observation value. **The value is always transmitted as a string**, regardless of the underlying data type. |

<br />

Applications should use the received message fields to determine the product, observation and value represented by each update.

***

# Connection Management

The SignalR connection should be treated as a long-running connection while the application requires real-time updates.

The client should:

* Establish the SignalR connection.
* Confirm that the connection has been established.
* Subscribe to the required products.
* Listen for incoming updates.
* Handle connection errors.
* Reconnect when the connection is interrupted.
* Stop the connection when streaming is no longer required.

<br />

***

# Starting the Stream

The general sequence for starting a stream is:

```text
1. Obtain authentication token
          │
          ▼
2. Create SignalR connection
          │
          ▼
3. Connect to products hub
          │
          ▼
4. Subscribe to products
          │
          ▼
5. Listen for ProductUpdate
```

Once connected, the client subscribes to each configured product.

***

# Stopping the Stream

When real-time updates are no longer required, the client should stop the SignalR connection.

The example implementation exposes a dedicated stop operation and handles process interruption to cleanly disconnect from the SignalR hub.

<br />

***

# Example Implementation

The following is an example implementation of a SignalR streaming client.

The example demonstrates:

* Establishing a SignalR connection.
* Authenticating the connection with an access token.
* Subscribing to products using serial numbers.
* Receiving `ProductUpdate` events.
* Handling connection and subscription errors.
* Automatically reconnecting.
* Stopping the connection cleanly.

The implementation language and client library are specific to the example; the SignalR concepts and interaction model described above are not language-specific.

## Example

```typescript
import { HubConnection, HubConnectionBuilder } from "@microsoft/signalr";

const token: string = "eyJhbGciOi...";

const subscriptions: string[] = ["EC2BE4KY", "ECMRWT4V"];

// Interface defining the structure of incoming messages from the IoT device
interface DeviceMessage {
  mid: string; // Device ID
  dataType: number; // Type of data (e.g., string, int)
  id: number; // Observation ID
  timestamp: string; // Timestamp of the data
  value: any; // The data value
}

// SignalRClient class manages connection to the SignalR hub and handles subscriptions
class SignalRClient {
  private connection: HubConnection;
  private readonly subscriptions: string[];

  // Constructor initializes the SignalRClient with subscriptions and token
  constructor(subscriptions: string[], private token: string) {
    this.subscriptions = subscriptions;

    this.connection = new HubConnectionBuilder()
      .withUrl("https://streams.easee.com/hubs/products", {
        accessTokenFactory: () => `${this.token}`, // Access token for authentication
      })
      .withAutomaticReconnect() // Configures automatic reconnection
      .build();

    // Sets up a listener for 'ProductUpdate' messages from the server
    this.connection.on("ProductUpdate", this.handleProductUpdate);
  }

  // Handler for processing incoming 'ProductUpdate' messages
  private handleProductUpdate(message: DeviceMessage): void {
    console.log("Received message:", message);
  }

  // Starts the SignalR connection and subscribes to each device
  public async start(): Promise<void> {
    try {
      await this.connection.start();
      console.log("SignalR Connected.");

      // Subscribes to each device in the subscriptions array
      for (const subscription of this.subscriptions) {
        await this.connection.invoke(
          "SubscribeWithCurrentState",
          subscription,
          true
        );

        console.log(`Subscribed to device: ${subscription}`);
      }
    } catch (err) {
      console.error(
        "Error while establishing connection or subscribing:",
        err
      );

      if (err instanceof Error) {
        console.error("Error details:", err.message);
      }
    }
  }

  // Stops the SignalR connection
  public async stop(): Promise<void> {
    try {
      await this.connection.stop();
      console.log("SignalR Disconnected.");
    } catch (err) {
      console.error("Error during disconnection:", err);
    }
  }
}

// Start stream example
const client = new SignalRClient(subscriptions, token);
client.start();

// Handle clean-up and errors (e.g., on pressing Ctrl+C in the terminal)
process.on("SIGINT", () => {
  client.stop();
});
```

# Example Output

A successful connection and subscription may be represented by output similar to:

```text
SignalR Connected.
Subscribed to device: EX2B54KY
Subscribed to device: EXMR5T4V
```

When an update is received:

```text
Received message: {
  mid: "...",
  dataType: ...,
  id: ...,
  timestamp: "...",
  value: ...
}
```

The exact data contained within an update depends on the observation being received.

***

# Summary

Easee SignalR Streaming provides a real-time mechanism for applications to receive data from subscribed products.

The integration follows a straightforward model:

**Authenticate → Connect → Subscribe → Receive → Process**

Products are identified by serial number, and a client can subscribe to multiple products. Incoming product updates are delivered through the `ProductUpdate` event and contain the defined observation fields.

For product commands and control operations, use the appropriate REST API rather than the SignalR streaming connection.