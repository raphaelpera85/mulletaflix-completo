import XCTest
@testable import MulletaFlixCore

final class SyncPlayRealtimeTests: XCTestCase {
    func testBuildsWebSocketURLWithAuthenticationAndDeviceMetadata() throws {
        let serverURL = try XCTUnwrap(URL(string: "https://example.test/base"))
        let url = try XCTUnwrap(SyncPlayRealtimeClient.webSocketURL(serverURL: serverURL, accessToken: "a&b", deviceID: "ios-1"))

        XCTAssertEqual(url.scheme, "wss")
        XCTAssertEqual(url.path, "/base/socket")
        XCTAssertTrue(url.query?.contains("api_key=a%26b") == true)
        XCTAssertTrue(url.query?.contains("deviceId=ios-1") == true)
    }

    func testDecodesSyncPlayGroupUpdateEnvelopeWithObjectPayload() throws {
        let data = #"{"MessageType":"SyncPlayGroupUpdate","Data":{"Type":"StateUpdate"}}"#.data(using: .utf8)!
        let event = try SyncPlayRealtimeDecoder.decode(data)

        XCTAssertEqual(event, .groupUpdate(SyncPlayGroupUpdate(type: "StateUpdate")))
    }

    func testUnknownMessageDoesNotBreakRealtimeDecoder() throws {
        let data = #"{"MessageType":"KeepAlive","Data":{}}"#.data(using: .utf8)!
        XCTAssertEqual(try SyncPlayRealtimeDecoder.decode(data), .unknown(type: "KeepAlive"))
    }

    func testDecodesSeekCommandPositionTicks() throws {
        let data = #"{"MessageType":"SyncPlayCommand","Data":{"Command":"Seek","PositionTicks":15000000}}"#.data(using: .utf8)!
        let event = try SyncPlayRealtimeDecoder.decode(data)

        XCTAssertEqual(event, .command(SyncPlayCommand(name: "Seek", positionTicks: 15000000)))
    }
}
