#pragma once

#include "OLRakPeerInterface.h"

#include <memory>

// Implements OLRakPeerInterface and relays commands to an AsyncSocket instance.
// The implementation makes some assumptions about the way the game uses the interface:
// - Only one peer is connected at a time. When the game wants to talk to a new peer, the current one is disconnected first.
// - The game always calls deallocatePacket on a received packet, before calling receive again. Only one packet is processed at a time.
class OLRakPeerAdapter : public OLRakPeerInterface
{
private:
	bool connected = false;
	
	OLPacket thePacket{
		OLPacket::unassignedSystemIndex,
		OLSystemAddress::unassigned,
		OLRakNetGUID::unassigned,
		0, 0, nullptr, false
	};
	bool thePacketWasReceived = false;

	static constexpr OLRakNetGUID connectedGUID = { 0x01, 0x00, 0x00, 0x00 };
	static constexpr uint16_t connectedSystemIndex = 0;

	std::unique_ptr<class AsyncSocket> socket;

public:
	explicit OLRakPeerAdapter(std::unique_ptr<class AsyncSocket> socket) : socket(std::move(socket)) { }

	virtual uint16_t numberOfConnections() override;

	virtual bool connect(char* host, uint16_t remotePort, char* passwordData, uint32_t passwordDataLenght, uint32_t connectionSocketIndex) override;

	virtual bool getConnectionList(OLSystemAddress* remoteSystems, uint16_t* numberOfSystems) override;

	virtual bool send2(OLBitStream* stream, uint32_t priority, uint32_t reliability, uint8_t orderingChannel, OLSystemAddress address, bool broadcast) override;

	virtual OLPacket* receive(void* arg) override;

	virtual void deallocatePacket(OLPacket* packet) override;

	virtual void closeConnection(OLSystemAddress target, bool sendDisconnectNotification, uint8_t orderingChannel) override;

	virtual bool isConnected(OLSystemAddress address, bool flag1, bool flag2) override;

	virtual OLSystemAddress* getSystemAddressFromIndex(OLSystemAddress* address, int index) override;
};