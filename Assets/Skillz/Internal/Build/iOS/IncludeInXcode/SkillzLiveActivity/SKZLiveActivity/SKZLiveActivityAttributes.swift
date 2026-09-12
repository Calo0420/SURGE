//
//  OfferAttributes.swift
//  SkillzSDK-iOS
//
//  Created by Sachin Agrawal on 12/04/25.
//  Copyright © 2025 Skillz. All rights reserved.
//

import ActivityKit
import Foundation

@available(iOS 15, *)
struct SKZLiveActivityAttributes: ActivityAttributes {
    public struct ContentState: Codable, Hashable {
        let minimalTopRightText: SKZLiveActivityAttributedString?
        let expendedTopRightText: SKZLiveActivityAttributedString?
        let expendedMainText: SKZLiveActivityAttributedString?
        let redirectUrl:URL?
        let buttonText:String?
        let imageURL:String?
        let endDate: Date?
        let appGroupIdentifier: String
    }
    var activityType: String?
}

struct SKZLiveActivityAttributedString:Codable,Hashable {
    struct SKZLiveActivityAttributedStyle:Codable, Hashable {
        let color: String?
        let fontSize: Int?
        let fontWeight: String?
    }
    let text: String
    let styles: [SKZLiveActivityAttributedStyle]
    let countDownTargets: [String]?
}
