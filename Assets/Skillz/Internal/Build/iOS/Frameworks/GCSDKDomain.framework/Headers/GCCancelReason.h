/*
 * © 2012-2023 GeoComply Solutions Inc.
 * All Rights Reserved.
 * NOTICE: All information contained herein is, and remains
 * the property of GeoComply Solutions Inc.
 * Dissemination, distribution, copying of this information or reproduction
 * of this material is strictly forbidden unless prior written permission
 * is obtained from GeoComply Solutions Inc.
 */

#import <Foundation/Foundation.h>

typedef NS_ENUM(NSUInteger, GCCancelReason) {
  GCCancelReasonNoReason,
  GCCancelReasonUserCancels,
  GCCancelReasonApplicationEntersBackground,
  GCCancelReasonRequestNotNeeded,
  GCCancelReasonLongRequest,
  GCCancelReasonOther
};



