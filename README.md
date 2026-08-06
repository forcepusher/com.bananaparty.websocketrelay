# com.bananaparty.websocketrelay  
  
Unity package. Fully cross-platform and portable WebSocket client and relay server library.  
  
Make sure you have standalone [Git](https://git-scm.com/downloads) installed first. Reboot after installation.  
In Unity, open "Window" -> "Package Manager".  
Click the "+" sign at the top left corner -> "Add package from git URL..."  
Paste this: `https://github.com/forcepusher/com.bananaparty.websocketrelay.git#2.2.0`  
To update the package, simply add it again using a different version tag.  
See minimum required Unity version in the `package.json` file.  
  
---  
  
Networking as simple as it gets (for programmers though).  
It's basically a peer-to-peer networking through a relay server, with wss:// support out of the box.  
The goal is to provide bare minimum to get things done while owning the entire backend stack.  
  
Key differences from Photon PUN:
1. No master client. Authority is either based on distance to the target or claimed manually.  
2. You host your own relay server and have full control of everything. I host mine on personal home server.  
3. It's based on [Bun's pub/sub](https://bun.com/docs/guides/websocket/pubsub) messaging channels, not rooms. You can subscribe to multiple channels.  
  
Key priorities:  
1. Developer Experience - JSON data stream for developing. Binary stream for shipping.  
2. Portable & Cheap - Relay server runtime embedded in Unity package. No expensive setups, doubleclick-ready.  
3. Tests & Stability - Integration tests using the portable runtime for quick QA. Especially valuable for AI slop.  
  
Future plans:  
1. Keep adding features and maintaining it. I don't have a choice coz I need a free Photon on a job and pet projects.  
2. Unity Instance Dedicated Server. Unity spins up a relay server and connects to it as a client to act as a server.  
3. UDP support via HTTP/3 QUIC. At this point it's going to be just as efficient as any other non-web network library.  
  
Not planned:  
1. Chasing performance brownie points. If something is not spiking in a profiler, then it will not be optimized.  
2. Drag and drop garbage. Too much hassle and bloat just to get right. If you're not a programmer - don't touch it.  
3. Deterministic prediction-rollback. Very CPU-intensive, expensive to develop, and horrible developer experience.  
  
---  
  
Library boilerplate code and tests were AI-assisted, while design decisions and OOP is done by hand.  
And as always - beware it's all code.  
