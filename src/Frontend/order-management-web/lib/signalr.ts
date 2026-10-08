import * as signalR from "@microsoft/signalr";
import { API_URL } from "@/lib/api";

export function createOrdersHubConnection(): signalR.HubConnection {
  return new signalR.HubConnectionBuilder()
    .withUrl(`${API_URL}/hubs/orders`)
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Warning)
    .build();
}
