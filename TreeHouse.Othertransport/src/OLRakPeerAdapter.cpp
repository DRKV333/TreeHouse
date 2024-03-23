#include "OLRakPeerAdapter.h"

#include "Log.h"
#include "Addrs.h"
#include "AsyncSocket.h"

uint16_t OLRakPeerAdapter::numberOfConnections()
{
	LOG_THIS_DEBUG("connected: %d", connected);
	return connected ? 1 : 0;
}

bool OLRakPeerAdapter::connect(char* host, uint16_t remotePort, char* passwordData, uint32_t passwordDataLenght, uint32_t connectionSocketIndex)
{
	LOG_THIS_DEBUG(L"%S:%u", host, remotePort);

	if (connected)
	{
		LOG_THIS_DEBUG(L"Already connected to %X:%u", thePacket.systemAddress.ip, thePacket.systemAddress.port);
		return false;
	}
	
	uint32_t ip[2];
	reinterpret_cast<void(__cdecl*)(uint32_t*, char*)>(Addrs::getptr(Addrs::parseIpV4))(ip, host);
	
	OLSystemAddress systemAddress{ .ip = ip[0], .port = remotePort };

	if (thePacket.systemAddress == systemAddress)
	{
		LOG_THIS_DEBUG(L"Already asked socket to connect, don't try again");
		return true;
	}

	thePacket.systemIndex = connectedSystemIndex;
	thePacket.systemAddress = systemAddress;
	thePacket.systemGUID = connectedGUID;

	socket->connect(ip[0], remotePort);

	return true;
}

bool OLRakPeerAdapter::getConnectionList(OLSystemAddress* remoteSystems, uint16_t* numberOfSystems)
{
	LOG_THIS_DEBUG("connected: %d", connected);

	if (connected)
	{
		if (remoteSystems != nullptr)
			*remoteSystems = thePacket.systemAddress;
		*numberOfSystems = 1;
	}
	else
	{
		*numberOfSystems = 0;
	}

	return true;
}

bool OLRakPeerAdapter::send2(OLBitStream* stream, uint32_t priority, uint32_t reliability, uint8_t orderingChannel, OLSystemAddress address, bool broadcast)
{
	if (!connected)
	{
		LOG_THIS_DEBUG(L"Not connected");
		return false;
	}

	if (address != thePacket.systemAddress)
	{
		LOG_THIS_DEBUG(L"Message sent to wrong address: %X:%u", address.ip, address.port);
		return false;
	}

	socket->send(stream->data, stream->getNumberOfBytesUsed());
	return true;
}

OLPacket* OLRakPeerAdapter::receive(void* arg)
{
	if (thePacketWasReceived)
	{
		LOG_THIS_DEBUG(L"Receive was called before deallocate.");
		return nullptr;
	}

	uint32_t length;
	char* data = socket->receive(&length);

	if (data == nullptr)
		return nullptr;

	if (length == 1 && data[0] == OLDefaultMessageIDTypes::OL_ID_CONNECTION_REQUEST_ACCEPTED)
	{
		connected = true;
		LOG_THIS_DEBUG(L"Socket signals successful connection.");
	}

	thePacket.data = data;
	thePacket.length = length;
	thePacket.lengthBits = length * 8;

	thePacketWasReceived = true;
	return &thePacket;
}

void OLRakPeerAdapter::deallocatePacket(OLPacket* packet)
{
	if (packet != &thePacket)
	{
		LOG_THIS_DEBUG(L"Tried to deallocate invalid packet: %X", packet);
		return;
	}

	if (!thePacketWasReceived)
	{
		LOG_THIS_DEBUG(L"Packet was not sent before deallocate.");
		return;
	}

	socket->receiveAck();
	thePacketWasReceived = false;
}

void OLRakPeerAdapter::closeConnection(OLSystemAddress target, bool sendDisconnectNotification, uint8_t orderingChannel)
{
	socket->disconnect();
	thePacket.systemIndex = OLPacket::unassignedSystemIndex;
	thePacket.systemAddress = OLSystemAddress::unassigned;
	thePacket.systemGUID = OLRakNetGUID::unassigned;
	connected = false;
	LOG_THIS_DEBUG("Disconnected");
}

bool OLRakPeerAdapter::isConnected(OLSystemAddress address, bool flag1, bool flag2)
{
	LOG_THIS_DEBUG(L"asked: %X:%u, actual: %X:%u", address.ip, address.port, thePacket.systemAddress.ip, thePacket.systemAddress.port);
	return connected && address == thePacket.systemAddress;
}

OLSystemAddress* OLRakPeerAdapter::getSystemAddressFromIndex(OLSystemAddress* address, int index)
{
	if (connected && index == 0)
		*address = thePacket.systemAddress;
	else
		*address = OLSystemAddress::unassigned;
	return address;
}
