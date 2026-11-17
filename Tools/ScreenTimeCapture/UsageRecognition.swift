import Foundation
import Vision

enum UsageRecognition {
    struct Entry: Encodable {
        let name: String
        let seconds: Int
    }

    private struct Text {
        let value: String
        let bounds: CGRect
    }

    private struct Usage {
        let label: Text
        let seconds: Int
        let timeColumn: CGFloat
    }

    private static let units = #"時間|分|秒|hours?|hrs?|h|min(?:ute)?s?|m|sec(?:ond)?s?|s"#
    private static let duration = try! NSRegularExpression(pattern: #"(?:^|\s)((?:[0-9０-９]+\s*(?:"# + units + #")\s*)+)$"#, options: .caseInsensitive)
    private static let component = try! NSRegularExpression(pattern: #"([0-9０-９]+)\s*("# + units + #")"#, options: .caseInsensitive)
    private static let appHeaders: Set<String> = ["アプリ", "app", "apps", "app or website"]
    private static let timeHeaders: Set<String> = ["時刻", "時間", "使用時間", "time", "usage", "duration"]
    private static let headings: Set<String> = [
        "合計", "すべてのアクティビティ", "すべてのアプリ", "すべての使用状況", "すべての使用時間", "1日の平均", "スクリーンタイム",
        "total", "all activity", "all usage", "all apps", "daily average", "usage", "screen time"
    ]

    static func topThree(in image: CGImage) throws -> [String] {
        try entries(in: image).prefix(3).map { "\($0.name)  \(format($0.seconds))" }
    }

    static func entries(in image: CGImage) throws -> [Entry] {
        var labels: [Text] = []
        var times: [(seconds: Int, bounds: CGRect)] = []
        for observation in try recognize(in: image) {
            guard let candidate = observation.topCandidates(1).first else { continue }
            let text = candidate.string
            let fullRange = NSRange(text.startIndex..., in: text)
            guard let match = duration.firstMatch(in: text, range: fullRange),
                  let timeRange = Range(match.range(at: 1), in: text),
                  let seconds = seconds(in: String(text[timeRange])) else {
                labels.append(Text(value: text, bounds: observation.boundingBox))
                continue
            }

            let bounds = try candidate.boundingBox(for: timeRange)?.boundingBox ?? observation.boundingBox
            times.append((seconds, bounds))
            if timeRange.lowerBound > text.startIndex {
                let nameRange = text.startIndex..<timeRange.lowerBound
                if let nameBounds = try candidate.boundingBox(for: nameRange)?.boundingBox {
                    labels.append(Text(value: String(text[nameRange]).trimmingCharacters(in: .whitespaces), bounds: nameBounds))
                }
            }
        }

        let timeHeader = labels.first { label in
            timeHeaders.contains(label.value.lowercased()) && label.bounds.minX >= 0.45 && labels.contains { app in
                appHeaders.contains(app.value.lowercased()) && app.bounds.minX >= 0.22
                    && app.bounds.maxX < label.bounds.minX && isSameRow(app.bounds, label.bounds)
            }
        }
        let names = labels.filter { label in
            label.bounds.minX >= 0.22 && !headings.contains(label.value.lowercased())
                && label.value.rangeOfCharacter(from: .letters) != nil
        }
        let rows: [Usage] = times.compactMap { time in
            guard time.bounds.minX >= 0.45 else { return nil }
            if let header = timeHeader,
               time.bounds.midY >= header.bounds.minY || abs(header.bounds.minX - time.bounds.minX) > 0.025 {
                return nil
            }
            let name = names.filter { name in
                isSameRow(name.bounds, time.bounds) && name.bounds.maxX <= time.bounds.minX + 0.005
            }.max { $0.bounds.maxX < $1.bounds.maxX }
            return name.map { Usage(label: $0, seconds: time.seconds, timeColumn: time.bounds.minX) }
        }

        // 見出しを読めない場合は、利用時間が最も多く並ぶ列を使う。
        var column: [Usage] = []
        for row in rows.sorted(by: { $0.timeColumn < $1.timeColumn }) {
            let aligned = rows.filter { abs($0.timeColumn - row.timeColumn) <= 0.025 }
            if aligned.count > column.count { column = aligned }
        }

        let leftEdges = column.map { $0.label.bounds.minX }.sorted()
        let labelLeftEdge = leftEdges.isEmpty ? 0 : leftEdges[leftEdges.count / 2]
        var seen = Set<String>()
        var entries: [Entry] = []
        for usage in column.sorted(by: { first, second in
            first.seconds == second.seconds ? first.label.value < second.label.value : first.seconds > second.seconds
        }) {
            let name = displayName(labelText(for: usage, in: image, labelLeftEdge: labelLeftEdge))
            guard seen.insert(name.lowercased()).inserted else { continue }
            entries.append(Entry(name: name, seconds: usage.seconds))
        }
        return entries
    }

    private static func recognize(in image: CGImage, region: CGRect? = nil) throws -> [VNRecognizedTextObservation] {
        let request = VNRecognizeTextRequest()
        request.recognitionLevel = .accurate
        request.recognitionLanguages = ["ja-JP", "en-US"]
        request.usesLanguageCorrection = false
        if let region { request.regionOfInterest = region }
        try VNImageRequestHandler(cgImage: image).perform([request])
        return request.results ?? []
    }

    private static func labelText(for usage: Usage, in image: CGImage, labelLeftEdge: CGFloat) -> String {
        let label = usage.label
        // アイコンまで文字として認識した行は、アプリ名の列から読み直す。
        guard label.bounds.minX < labelLeftEdge - 0.015 else { return label.value }
        let left = labelLeftEdge - 0.006
        let bottom = max(0, label.bounds.minY - label.bounds.height * 0.2)
        let top = min(1, label.bounds.maxY + label.bounds.height * 0.2)
        let right = usage.timeColumn - 0.008
        guard left < right else { return label.value }
        let region = CGRect(x: left, y: bottom, width: right - left, height: top - bottom)
        let observations = try? recognize(in: image, region: region)
        return observations?.compactMap { $0.topCandidates(1).first?.string }.first ?? label.value
    }

    private static func isSameRow(_ first: CGRect, _ second: CGRect) -> Bool {
        abs(first.midY - second.midY) <= max(first.height, second.height) * 0.6
    }

    private static func displayName(_ value: String) -> String {
        let name = value.trimmingCharacters(in: .whitespacesAndNewlines)
        if ["…", ".", "・"].contains(where: name.hasSuffix), let subtitle = name.range(of: " -") {
            return String(name[..<subtitle.lowerBound])
        }
        return name
    }

    private static func seconds(in value: String) -> Int? {
        let text = value.folding(options: .widthInsensitive, locale: nil).lowercased()
        let matches = component.matches(in: text, range: NSRange(text.startIndex..., in: text))
        var seconds = 0
        for match in matches {
            guard let number = Range(match.range(at: 1), in: text), let count = Int(text[number]), count <= 604_800,
                  let unit = Range(match.range(at: 2), in: text) else { return nil }
            switch text[unit].first {
            case "h", "時": seconds += count * 3600
            case "m", "分": seconds += count * 60
            default: seconds += count
            }
            if seconds > 604_800 { return nil }
        }
        return matches.isEmpty ? nil : seconds
    }

    private static func format(_ seconds: Int) -> String {
        var parts: [String] = []
        if seconds >= 3600 { parts.append("\(seconds / 3600)時間") }
        let minutes = seconds / 60 % 60
        if minutes > 0 { parts.append("\(minutes)分") }
        if seconds % 60 > 0 || parts.isEmpty { parts.append("\(seconds % 60)秒") }
        return parts.joined(separator: " ")
    }
}
