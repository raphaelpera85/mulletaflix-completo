// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "MulletaFlix-iOS",
    platforms: [.iOS(.v18)],
    products: [
        .library(name: "MulletaFlixCore", targets: ["MulletaFlixCore"]),
    ],
    targets: [
        .target(name: "MulletaFlixCore"),
        .testTarget(name: "MulletaFlixCoreTests", dependencies: ["MulletaFlixCore"]),
    ]
)
