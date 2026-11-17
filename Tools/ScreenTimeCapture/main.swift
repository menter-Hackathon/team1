import AppKit
import Darwin
import ImageIO
import ScreenCaptureKit
import UniformTypeIdentifiers

@available(macOS 15.2, *)
final class ScreenTimeCapture: NSObject, NSApplicationDelegate, NSWindowDelegate, SCContentSharingPickerObserver {
    private static let settingsBundleID = "com.apple.systempreferences"
    private let destination: URL?
    private let settingsURL: URL?
    private let caller: NSRunningApplication?
    private let picker = SCContentSharingPicker.shared
    private var guide: NSPanel?
    private var capturing = false
    private var finished = false

    init(destination: URL?, settingsURL: URL?) {
        self.destination = destination
        self.settingsURL = settingsURL
        caller = settingsURL == nil ? nil : NSRunningApplication(processIdentifier: getppid())
    }

    func applicationDidFinishLaunching(_ notification: Notification) {
        guard let settingsURL else {
            showPicker()
            return
        }
        if !NSWorkspace.shared.open(settingsURL) {
            finish(status: 1, message: "スクリーンタイムを開けませんでした。")
            return
        }
        showGuide()
    }

    private func showGuide() {
        let panel = NSPanel(
            contentRect: NSRect(x: 0, y: 0, width: 360, height: 154),
            styleMask: [.titled, .closable, .nonactivatingPanel],
            backing: .buffered,
            defer: false
        )
        panel.title = "端末スキャン"
        panel.delegate = self
        panel.isFloatingPanel = true
        panel.hidesOnDeactivate = false
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        panel.appearance = NSAppearance(named: .aqua)

        let instructions = NSTextField(wrappingLabelWithString: "このMacの「先週」のアプリ一覧を表示してください。\n「痕跡を読み取る」を押したら、共有画面で「システム設定」を選びます。")
        let scan = NSButton(title: "痕跡を読み取る", target: self, action: #selector(showPicker))
        scan.bezelStyle = .rounded
        scan.bezelColor = NSColor(srgbRed: 0.76, green: 0.09, blue: 0.17, alpha: 1)
        let stack = NSStackView(views: [instructions, scan])
        stack.orientation = .vertical
        stack.alignment = .leading
        stack.spacing = 12
        stack.edgeInsets = NSEdgeInsets(top: 16, left: 16, bottom: 16, right: 16)
        panel.contentView = stack
        instructions.widthAnchor.constraint(equalTo: stack.widthAnchor, constant: -32).isActive = true
        if let screen = NSScreen.main?.visibleFrame {
            panel.setFrameOrigin(NSPoint(x: screen.maxX - panel.frame.width - 20, y: screen.minY + 20))
        }

        guide = panel
        panel.orderFrontRegardless()
    }

    func windowWillClose(_ notification: Notification) {
        finish(status: 2)
    }

    @objc private func showPicker() {
        guide?.orderOut(nil)
        var configuration = SCContentSharingPickerConfiguration()
        configuration.allowedPickerModes = [.singleWindow]
        if settingsURL != nil {
            configuration.excludedBundleIDs = NSWorkspace.shared.runningApplications.compactMap(\.bundleIdentifier)
                .filter { $0 != Self.settingsBundleID }
        }
        picker.defaultConfiguration = configuration
        picker.add(self)
        picker.isActive = true
        NSApplication.shared.activate()
        picker.present(using: .window)
    }

    func contentSharingPicker(_ picker: SCContentSharingPicker, didUpdateWith filter: SCContentFilter, for stream: SCStream?) {
        DispatchQueue.main.async {
            guard !self.capturing else { return }
            self.capturing = true
            self.capture(filter)
        }
    }

    func contentSharingPicker(_ picker: SCContentSharingPicker, didCancelFor stream: SCStream?) {
        DispatchQueue.main.async { self.finish(status: 2) }
    }

    func contentSharingPickerStartDidFailWithError(_ error: Error) {
        DispatchQueue.main.async { self.finish(status: 1, message: error.localizedDescription) }
    }

    private func capture(_ filter: SCContentFilter) {
        guard filter.includedWindows.count == 1,
              filter.includedWindows.first?.owningApplication?.bundleIdentifier == Self.settingsBundleID else {
            finish(status: 1, message: "共有するウィンドウに「システム設定」を選んでください。")
            return
        }

        let configuration = SCStreamConfiguration()
        let size = filter.contentRect.size
        let scale = CGFloat(filter.pointPixelScale)
        configuration.width = max(1, Int((size.width * scale).rounded(.up)))
        configuration.height = max(1, Int((size.height * scale).rounded(.up)))
        configuration.showsCursor = false
        configuration.ignoreShadowsSingleWindow = true

        SCScreenshotManager.captureImage(contentFilter: filter, configuration: configuration) { image, error in
            guard let image else {
                self.finish(status: 1, message: error?.localizedDescription ?? "画像を取得できませんでした。")
                return
            }
            if let destination = self.destination {
                guard writePNG(image, to: destination) else {
                    self.finish(status: 1, message: "PNGを保存できませんでした。")
                    return
                }
            }
            do {
                try printUsage(in: image, asJSON: self.destination == nil)
                self.finish(status: 0)
            } catch {
                self.finish(status: 3, message: error.localizedDescription)
            }
        }
    }

    private func finish(status: Int32, message: String? = nil) {
        DispatchQueue.main.async {
            guard !self.finished else { return }
            self.finished = true
            self.guide?.orderOut(nil)
            self.picker.remove(self)
            self.picker.isActive = false
            if let message { FileHandle.standardError.write(Data((message + "\n").utf8)) }
            if let caller = self.caller {
                NSApplication.shared.yieldActivation(to: caller)
                if !caller.activate(from: NSRunningApplication.current, options: []) {
                    caller.activate(options: [])
                }
            }
            exit(status)
        }
    }
}

func writePNG(_ image: CGImage, to destination: URL) -> Bool {
    guard let writer = CGImageDestinationCreateWithURL(destination as CFURL, UTType.png.identifier as CFString, 1, nil) else { return false }
    CGImageDestinationAddImage(writer, image, nil)
    return CGImageDestinationFinalize(writer)
}

func printUsage(in image: CGImage, asJSON: Bool) throws {
    if asJSON {
        let data = try JSONEncoder().encode(UsageRecognition.entries(in: image))
        FileHandle.standardOutput.write(data)
        return
    }
    for (index, usage) in try UsageRecognition.topThree(in: image).enumerated() {
        print("\(index + 1). \(usage)")
    }
}

guard #available(macOS 15.2, *) else {
    FileHandle.standardError.write(Data("撮影にはmacOS 15.2以降が必要です。\n".utf8))
    exit(1)
}
if CommandLine.arguments.count == 3, ["--recognize", "--recognize-json"].contains(CommandLine.arguments[1]) {
    let url = URL(fileURLWithPath: CommandLine.arguments[2])
    guard let source = CGImageSourceCreateWithURL(url as CFURL, nil),
          let image = CGImageSourceCreateImageAtIndex(source, 0, nil) else { exit(1) }
    do {
        try printUsage(in: image, asJSON: CommandLine.arguments[1] == "--recognize-json")
        exit(0)
    } catch {
        FileHandle.standardError.write(Data((error.localizedDescription + "\n").utf8))
        exit(3)
    }
}
let args = CommandLine.arguments
let destination: URL?
let settingsURL: URL?
if args.count == 3, args[1] == "--scan", let url = URL(string: args[2]), url.scheme == "x-apple.systempreferences" {
    destination = nil
    settingsURL = url
} else if args.count == 2, !args[1].hasPrefix("--") {
    destination = URL(fileURLWithPath: args[1])
    settingsURL = nil
} else {
    exit(1)
}
let application = NSApplication.shared
let capture = ScreenTimeCapture(destination: destination, settingsURL: settingsURL)
application.setActivationPolicy(.accessory)
application.delegate = capture
withExtendedLifetime(capture) { application.run() }
