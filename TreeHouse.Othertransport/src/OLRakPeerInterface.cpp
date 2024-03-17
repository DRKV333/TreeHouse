#include "OLRakPeerInterface.h"
#include "Log.h"

void OLRakPeerInterface::unimplementedPurecall(const char* name)
{
	LOG_THIS_DEBUG(L"Called into unimplemented RakPeerInterface function: %S Client will probably crash now...", name);
	DebugBreak();
}