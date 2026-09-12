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

NS_ASSUME_NONNULL_BEGIN

/**
 The 7xx constants indicate the type of error that resulted after a MyIP request.
 The 6xx constants are returned when checking license and loading MyIP configuration. @see GCClientError.
 */
typedef NS_ENUM(NSUInteger, GCMyIpErrorCode) {
    GCMyIpErrorServiceStopped       = 700, // Confirm MyIP service stopped
    GCMyIpErrorNoLicense            = 701, // License is not set
    GCMyIpErrorServiceDisabled      = 702, // MyIP service configuration is not defined
    GCMyIpErrorSSLFailed            = 703, // SSL error
    GCMyIpErrorInvalidIP            = 704, // Invalid IP address format
    GCMyIpErrorNetworkConnection    = 705, // Network connection error
    GCMyIpErrorInvalidHttpStatus    = 706, // MyIP service error with HTTP status is not 200
    GCMyIpErrorHostUnreachable      = 707, // MyIP host is unreachable
    GCMyIPErrorInvalidCarbonUrl     = 708, // Wrong format or missing Carbon URL
    GCMyIpErrorTimedOut             = 710, // MyIP service is timed out
    GCMyIpErrorUnknown              = 720, // Unknown error
};

/**
 The GCMyIpError object manages information about the details of a failed MyIP request.
 GCMyIpError will be returned via the failureHandler from the -startMyIpServiceWithSuccess:failure: method.
 
 @see -startMyIpServiceWithSuccess:failure:.
 */
@interface GCMyIpError : NSObject

/**
 Returns error code when a MyIP request failed.
 
 @see GCMyIpErrorCode, GCClientError.
 */
@property (nonatomic, readonly) GCMyIpErrorCode code;

/**
 Returns description when a MyIP request failed.
 */
@property (nonatomic, readonly, nullable) NSString *description;

/**
 Returns timestamp in milliseconds when a MyIP request failed.
 */
@property (nonatomic, readonly) NSTimeInterval timestamp;

- (instancetype)initWithCode:(NSInteger)code description:(NSString *)description;

@end

NS_ASSUME_NONNULL_END
