#include "TCPSocket.h"

#include "OLRakNetTypes.h"
#include "Log.h"

void TCPSocket::connect(uint32_t ip, uint16_t port)
{
	asio::co_spawn(strand, connectAsync(ip, port), asio::detached);
}

asio::awaitable<void> TCPSocket::connectAsync(uint32_t ip, uint16_t port)
{
	asio::ip::tcp::endpoint endpoint(asio::ip::address_v4(_byteswap_ulong(ip)), port);
	
	//TODO: Make this timeout a little sooner.
	try
	{
		co_await socket.async_connect(endpoint, asio::use_awaitable);
	}
	catch (const std::system_error& e)
	{
		LOG_DEBUG(L"Connect failed! %S", e.what());
		Buffer responseBuffer(1);
		responseBuffer.data()[0] = OLDefaultMessageIDTypes::OL_ID_CONNECTION_ATTEMPT_FAILED;
		inbox.enqueue(std::move(responseBuffer));
	}

	LOG_DEBUG(L"Connect success!");
	Buffer responseBuffer(1);
	responseBuffer.data()[0] = OLDefaultMessageIDTypes::OL_ID_CONNECTION_REQUEST_ACCEPTED;
	inbox.enqueue(std::move(responseBuffer));

	try
	{
		while (true)
		{
			Buffer::length_t length;
			co_await asio::async_read(socket, asio::mutable_buffer(&length, sizeof(length)), asio::use_awaitable);
		
			Buffer buffer(length);
			co_await asio::async_read(socket, asio::mutable_buffer(buffer.data(), buffer.length()), asio::use_awaitable);

			inbox.enqueue(std::move(buffer));
		}
	}
	catch (const std::system_error& e)
	{
		LOG_DEBUG(L"Read failed! %S", e.what());
		// TODO: Figure out how to tell the client about the disconnect.
	}
}

void TCPSocket::disconnect()
{
	asio::post(strand, [this]()
	{
		socket.shutdown(asio::socket_base::shutdown_both);
		socket.close();
	});
}

void TCPSocket::send(const char* data, uint32_t length)
{
	Buffer buffer(length);
	memcpy(buffer.data(), data, length);
	outbox.enqueue(std::move(buffer));

	asio::post(strand, [this]()
	{
		if (!writing)
		{
			writing = true;
			asio::co_spawn(strand, writeAsync(), asio::detached);
		}
	});
}

asio::awaitable<void> TCPSocket::writeAsync()
{
	try
	{
		while (true)
		{
			Buffer buffer;
			if (!outbox.try_dequeue(buffer))
				break;

			Buffer::length_t length = buffer.length();
			co_await asio::async_write(socket, asio::const_buffer(&length, sizeof(length)), asio::use_awaitable);
			co_await asio::async_write(socket, asio::const_buffer(buffer.data(), buffer.length()), asio::use_awaitable);
		}
	}
	catch (const std::system_error& e)
	{
		LOG_DEBUG(L"Write failed! %S", e.what());
	}

	writing = false;
}

char* TCPSocket::receive(uint32_t* length)
{
	if (!inbox.try_dequeue(pendingReceive))
		return nullptr;

	*length = pendingReceive.length();
	return pendingReceive.data();
}

void TCPSocket::receiveAck()
{
	pendingReceive = Buffer();
}
