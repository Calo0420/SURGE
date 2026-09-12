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
/**
 The GCCustomFields object contains extra information about a geolocation request.
 
 @warning If no GCCustomFields instance is not set to GeoComplyClient object. There's
 not any custom field is included in geolocation request.
 */
@interface GCCustomFields : NSObject
/**
 Sets a new value for a key in GCCustomFields instance. If a key doesn't exist, it will
 be created.
 
 @param fieldValue New value. Unless value is nil, in which case send -removeValueForKey:
 @param fieldKey Key needs to set value. This must not be nil.
 */
- (NSError* _Nullable)setValue:(NSString* _Nullable)fieldValue forKey:(NSString* _Nonnull)fieldKey;

/**
 Returns value for a key.
 
 @return Value for input key.s
 
 @param fieldKey Field that needs to retrieve value.
 */
- (nullable NSString*)valueForKey:(nonnull NSString*)fieldKey;

/**
 Returns all keys in GCCustomFields instance.
 
 @return An array of keys.
 */
- (nullable NSArray*)allKeys;

/**
 Remove a key and its value from GCCustomFields instance.
 
 @param fieldKey Field that needs to be removed.
 */
- (void)removeValueForKey:(nonnull NSString*)fieldKey;

/**
 Remove all key and values from GCCustomFields instance.
 */
- (void)removeAllValues;

@end
