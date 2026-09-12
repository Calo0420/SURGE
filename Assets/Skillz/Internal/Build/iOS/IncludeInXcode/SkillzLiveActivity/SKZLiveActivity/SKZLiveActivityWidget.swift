//
//  SKZLiveActivityWidget.swift
//  SkillzSDK-iOS
//
//  Created by Sachin Agrawal on 12/04/25.
//  Copyright © 2025 Skillz. All rights reserved.
//

import ActivityKit
import WidgetKit
import SwiftUI

@available(iOS 16.2, *)
struct SKZLiveActivityWidget: Widget {
    var body: some WidgetConfiguration {
        ActivityConfiguration(for: SKZLiveActivityAttributes.self) { context in
            SKZLiveActivityView(context: context)
        } dynamicIsland: { context in
            DynamicIsland {
                DynamicIslandExpandedRegion(.leading) {
                    DynamicGroupImage(fileName: context.state.imageURL, appGroupID: context.state.appGroupIdentifier).frame(width: 75, height: 75).aspectRatio(contentMode: .fill).cornerRadius(16).padding(.top,4)
                }
                
                DynamicIslandExpandedRegion(.trailing) {
                    SKZAttributedLabel(attributedText: context.state.expendedTopRightText)
                        .frame(maxWidth: .infinity)
                        .multilineTextAlignment(.center)
                }
                DynamicIslandExpandedRegion(.center) {
                    SKZAttributedLabel(attributedText: context.state.expendedMainText)
                        .lineLimit(2)
                        .fixedSize(horizontal: false, vertical: true)
                        .multilineTextAlignment(.leading).padding(.leading, 8).padding(.top, 8)
                }
                
                DynamicIslandExpandedRegion(.bottom) {
                    SKZDeeplinkButton(url: context.state.redirectUrl, buttonText: context.state.buttonText).padding(.bottom,4)
                }
                
            } compactLeading: {
                DynamicGroupImage(fileName: context.state.imageURL, appGroupID: context.state.appGroupIdentifier).cornerRadius(4).padding(.leading,4)
            } compactTrailing: {
                SKZAttributedLabel(attributedText: context.state.minimalTopRightText)
                    .frame(width: 60)
            } minimal: {
                DynamicGroupImage(fileName: context.state.imageURL, appGroupID: context.state.appGroupIdentifier).cornerRadius(4)
            }
            .widgetURL(context.state.redirectUrl)
        }
    }
}

