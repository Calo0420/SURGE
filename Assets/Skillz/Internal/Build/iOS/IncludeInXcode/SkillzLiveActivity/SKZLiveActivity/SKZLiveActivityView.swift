//
//  OfferLiveActivityView.swift
//  SkillzSDK-iOS
//
//  Created by Sachin Agrawal on 12/04/25.
//  Copyright © 2025 Skillz. All rights reserved.
//

import SwiftUI
import ActivityKit
import WidgetKit

struct SKZLiveActivityView: View {
    let context: ActivityViewContext<SKZLiveActivityAttributes>
    
    var body: some View {
        VStack(alignment: .leading) {
            HStack{
                DynamicGroupImage(fileName: context.state.imageURL, appGroupID: context.state.appGroupIdentifier).frame(width: 75, height: 75).aspectRatio(contentMode: .fill).cornerRadius(16)
                
                VStack(alignment: .leading){
                    SKZAttributedLabel(attributedText: context.state.expendedTopRightText)
                        .padding(.bottom, 18)
                        .lineLimit(nil)
                        .multilineTextAlignment(.trailing)
                    
                    SKZAttributedLabel(attributedText: context.state.expendedMainText)
                        .lineLimit(nil)
                        .fixedSize(horizontal: false, vertical: true)
                        .multilineTextAlignment(.leading).padding(.leading, 24)
                }
            }
            SKZDeeplinkButton(url: context.state.redirectUrl, buttonText: context.state.buttonText)
            
        }
        .padding(.horizontal, 36)
        .padding(.vertical, 18)
        .background(Color.black)
        .widgetURL(context.state.redirectUrl)
        
    }
}

/// `SKZAttributedLabel` displays an attributed string with customizable styles and supports multiple countdown timers.
///
/// ### String Creation
/// - **Structure**: Use `||` to separate segments. Example: `"Hello || {TIMER} || World || {TIMER}"`.
/// - **Styles**: Define styles (e.g., `fontSize`, `fontWeight`, `color`) for each segment.
/// - **Timers**: Use `{TIMER}` for countdowns. Each `{TIMER}` corresponds to a target date in `countDownTargets` (ISO8601 format).
///
/// ### Multiple Timers
/// - If the string contains multiple `{TIMER}` placeholders, they are replaced sequentially with countdown timers.
/// - Ensure the `countDownTargets` array has enough dates to match the number of `{TIMER}` placeholders.
///
/// ### Rendering
/// - Splits `text` into segments using `||`.
/// - Styles each segment using `styles`.
/// - Replaces each `{TIMER}` with a countdown timer using the corresponding target date.
///
/// ### Example
/// ```swift
/// let attributedString = SKZLiveActivityAttributedString(
///     text: "Event starts in: || {TIMER} || Ends in: || {TIMER}",
///     styles: [
///         SKZStyle(fontSize: 16, fontWeight: "regular", color: "#FFFFFF"),
///         SKZStyle(fontSize: 14, fontWeight: "bold", color: "#FF0000"),
///         SKZStyle(fontSize: 16, fontWeight: "regular", color: "#00FF00"),
///         SKZStyle(fontSize: 14, fontWeight: "bold", color: "#0000FF")
///     ],
///     countDownTargets: ["2025-12-31T23:59:59Z", "2026-01-01T23:59:59Z"]
/// )
/// SKZAttributedLabel(attributedText: attributedString)
/// ```
struct SKZAttributedLabel: View {
    let attributedText: SKZLiveActivityAttributedString?
    
    struct Segment: Identifiable {
        let id = UUID()
        let view: AnyView
    }
    
    var segments: Text {
        guard let attributedText = attributedText else { return Text("") }
        let parts = attributedText.text.components(separatedBy: "||")
        let styles = attributedText.styles
        let countdownTargets = attributedText.countDownTargets ?? []
        
        var result = Text("")
        var timerIndex = 0
        
        for (index, segment) in parts.enumerated() {
            let style = styles[safe: index]
            let fontSize = CGFloat(style?.fontSize ?? 14)
            let weight: Font.Weight = (style?.fontWeight?.lowercased() == "bold") ? .bold : .regular
            let color = Color(hex: style?.color ?? "#FFFFFF")
            
            if segment.contains("{TIMER}"),
               timerIndex < countdownTargets.count,
               let targetDate = ISO8601DateFormatter().date(from: countdownTargets[timerIndex]) {
                
                result = result + Text(timerInterval: Date()...targetDate, countsDown: true)
                    .foregroundColor(color)
                    .font(.system(size: fontSize, weight: weight))
                timerIndex += 1
            } else {
                result = result + Text(segment)
                    .foregroundColor(color)
                    .font(.system(size: fontSize, weight: weight))
            }
        }
        
        return result
    }
    
    var body: some View {
        segments
    }
}

struct DynamicGroupImage: View {
    let fileName: String?
    let appGroupID: String
    
    // Load UIImage from file URL in app group container
    private func loadUIImage() -> UIImage? {
        guard let fileName = fileName else {
            return nil
        }
        
        guard let container = FileManager.default.containerURL(forSecurityApplicationGroupIdentifier: appGroupID) else {
            return nil
        }
        let fileURL = container.appendingPathComponent(fileName)
        return UIImage(contentsOfFile: fileURL.path)
    }
    
    var body: some View {
        if let uiImage = loadUIImage() {
            Image(uiImage: uiImage).resizable()
                .scaledToFill()
            
        } else {
            Image(systemName: "gamecontroller.fill")
                .resizable()
                .scaledToFit()
                .foregroundColor(.white)
        }
    }
}

extension Color {
    init(hex: String) {
        let hex = hex.trimmingCharacters(in: CharacterSet.alphanumerics.inverted)
        var int: UInt64 = 0
        Scanner(string: hex).scanHexInt64(&int)
        let r = Double((int >> 16) & 0xFF) / 255
        let g = Double((int >> 8) & 0xFF) / 255
        let b = Double(int & 0xFF) / 255
        self.init(red: r, green: g, blue: b)
    }
}
extension Array {
    subscript(safe index: Int) -> Element? {
        return indices.contains(index) ? self[index] : nil
    }
}

struct SKZDeeplinkButton: View {
    let url: URL?
    let buttonText: String?
    
    var body: some View {
        Text(buttonText ?? "Play Now")
            .font(.system(size: 16, weight: .semibold))
            .foregroundColor(.white)
            .padding()
            .frame(maxWidth: .infinity, maxHeight: 25)
            .background(Color(red: 0.04, green: 0.71, blue: 0.05))
            .cornerRadius(44).widgetURL(url)
    }
}
