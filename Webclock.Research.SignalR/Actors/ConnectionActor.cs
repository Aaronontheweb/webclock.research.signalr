﻿using Akka.Actor;
using System.Diagnostics;
using Webclock.Research.SignalR.Messages;
using Webclock.Research.SignalR.Services;

namespace Webclock.Research.SignalR.Actors
{
    public class ConnectionActor : ReceiveActor
    {
        private readonly string _connectionId;
        private readonly string _type;
        private readonly ISignalRProcessor _webSocketToConnector;

        public ConnectionActor(string connectionId, string type, ISignalRProcessor webSocketToConnector)
        {
            _connectionId = connectionId;
            _type = type;
            _webSocketToConnector = webSocketToConnector;

            Receive<StartClockRequestByMonitor>(m => HandleStartClockRequestByMonitor(m));
            Receive<AddBid>(m => HandleAddBid(m));
            Receive<BuyIntentionQuit>(m => HandleBuyIntentionQuit(m));
        }

        private void HandleStartClockRequestByMonitor(StartClockRequestByMonitor startClockRequestByMonitor)
        {
            var serverConnectionActorSendTimestamp = Stopwatch.GetTimestamp();
            _webSocketToConnector.BroadcastStartClock(new StartClock(serverConnectionActorSendTimestamp)).Wait();
        }

        private void HandleAddBid(AddBid addBid)
        {
            _webSocketToConnector.BroadcastBidReceived(new BidReceived(addBid.ClientTimeSinceClockStartMs)).Wait();
        }

        private void HandleBuyIntentionQuit(BuyIntentionQuit buyIntentionQuit)
        {
            var serverConnectionActorReceiveTimestamp = Stopwatch.GetTimestamp();
            var serverTimeSinceClockStartMs = (serverConnectionActorReceiveTimestamp - buyIntentionQuit.ServerConnectionActorSendTimestamp) / (Stopwatch.Frequency / 1000);

            var fullLatency = serverTimeSinceClockStartMs - buyIntentionQuit.ClientTimeSinceClockStartMs;
            var serverSendLatency = (buyIntentionQuit.ServerAuctionHubSendTimestamp - buyIntentionQuit.ServerConnectionActorSendTimestamp) / (Stopwatch.Frequency / 1000);
            var serverReceiveLatency = (serverConnectionActorReceiveTimestamp - buyIntentionQuit.ServerAuctionHubReceiveTimestamp) / (Stopwatch.Frequency / 1000);

            _webSocketToConnector.AddLatency(buyIntentionQuit.ConnectionId, new Latency(
                buyIntentionQuit.ConnectionId,
                serverTimeSinceClockStartMs,
                buyIntentionQuit.ClientTimeSinceClockStartMs,
                fullLatency,
                serverSendLatency,
                serverReceiveLatency));
        }

        public static Props Props(string connectionId, string type, ISignalRProcessor webSocketToConnector)
        {
            return Akka.Actor.Props.Create(() => new ConnectionActor(connectionId, type, webSocketToConnector));
        }
    }
}
