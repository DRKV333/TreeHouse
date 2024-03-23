#pragma once

#include <cstdint>

// A convenient interface for reimplementing the functionality of a RakPeerInterface with async IO.
// Completion of operations should be signaled by sending single byte packets containing members of OLDefaultMessageIDTypes.
class AsyncSocket
{
public:
	// Start connection to an IPv4 address and port.
	// Connection success should be signaled by OL_ID_CONNECTION_REQUEST_ACCEPTED
	// and failure by OL_ID_CONNECTION_ATTEMPT_FAILED. Unexpected disconnection should be
	// reported by OL_ID_CONNECTION_LOST. This won't be called again while the socket is
	// still connected.
	// ip is in network byte order.
	virtual void connect(uint32_t ip, uint16_t port) = 0;

	// Request the socket to gracefully break connection. Success should be signaled
	// by OL_ID_DISCONNECTION_NOTIFICATION. This will only be called while the socket
	// is connected.
	virtual void disconnect() = 0;

	// Send a length byte long packet, starting at data. The data is only available
	// until the function returns. This will only be called while the socket is connected.
	virtual void send(const char* data, uint32_t length) = 0;

	// This function should return any packets received from the socket, as well as internal
	// notifications about operation completion. length should be set to the number of bytes
	// pointed to by the returned value. If no new data is available, the function should
	// return nullptr. Returned data should be available until receiveAck is called.
	// This function will only be called while the socket is connected, and it won't be called
	// again until receiveAck is called.
	virtual char* receive(uint32_t* length) = 0;

	// This function is called once the received data no longer needs to be available.
	virtual void receiveAck() { };

	virtual ~AsyncSocket() = default;
};