#pragma once

#include "AsyncSocket.h"

#include <memory>
#include <asio/io_context_strand.hpp>
#include <asio/ip/tcp.hpp>
#include <readerwriterqueue/readerwriterqueue.h>

class TCPSocket : public AsyncSocket
{
private:
	class Buffer
	{
	public:
		using length_t = uint32_t;

	private:
		length_t _length = 0;
		std::unique_ptr<char[]> _data;

	public:
		Buffer() = default;

		explicit Buffer(length_t lenght) : _length(lenght), _data(std::make_unique<char[]>(lenght)) { }

		length_t length() const { return _length; }

		char* data() { return _data.get(); }
		
		const char* data() const { return _data.get(); }

		asio::mutable_buffer mutable_buffer() { return asio::mutable_buffer(_data.get(), _length); }

		asio::const_buffer const_bugger() const { return asio::const_buffer(_data.get(), _length); }
	};

	asio::strand<asio::io_context::executor_type> strand;
	asio::ip::tcp::socket socket;

	moodycamel::ReaderWriterQueue<Buffer> inbox;
	moodycamel::ReaderWriterQueue<Buffer> outbox;

	Buffer pendingReceive;

	bool writing = false;

	asio::awaitable<void> connectAsync(uint32_t ip, uint16_t port);
	asio::awaitable<void> writeAsync();

public:
	explicit TCPSocket(asio::io_context& context)
		: strand(context.get_executor()), socket(context) { };

	virtual void connect(uint32_t ip, uint16_t port) override;

	virtual void disconnect() override;

	virtual void send(const char* data, uint32_t length) override;

	virtual char* receive(uint32_t* length) override;

	virtual void receiveAck() override;
};