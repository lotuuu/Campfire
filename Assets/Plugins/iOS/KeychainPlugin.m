#import <Foundation/Foundation.h>
#import <Security/Security.h>

static NSString* ServiceName(void) {
    return [[NSBundle mainBundle] bundleIdentifier] ?: @"com.garden.campfire";
}

static NSMutableDictionary* BaseQuery(NSString* key) {
    return [@{
        (__bridge id)kSecClass:       (__bridge id)kSecClassGenericPassword,
        (__bridge id)kSecAttrService: ServiceName(),
        (__bridge id)kSecAttrAccount: key
    } mutableCopy];
}

void _KeychainSet(const char* cKey, const char* cValue) {
    NSString* key   = [NSString stringWithUTF8String:cKey];
    NSData*   data  = [[NSString stringWithUTF8String:cValue]
                        dataUsingEncoding:NSUTF8StringEncoding];

    NSMutableDictionary* query = BaseQuery(key);

    // Delete any existing entry first
    SecItemDelete((__bridge CFDictionaryRef)query);

    query[(__bridge id)kSecValueData] = data;
    query[(__bridge id)kSecAttrAccessible] =
        (__bridge id)kSecAttrAccessibleAfterFirstUnlock;

    SecItemAdd((__bridge CFDictionaryRef)query, NULL);
}

const char* _KeychainGet(const char* cKey) {
    NSString* key = [NSString stringWithUTF8String:cKey];
    NSMutableDictionary* query = BaseQuery(key);
    query[(__bridge id)kSecReturnData]  = @YES;
    query[(__bridge id)kSecMatchLimit]  = (__bridge id)kSecMatchLimitOne;

    CFDataRef dataRef = NULL;
    OSStatus status = SecItemCopyMatching((__bridge CFDictionaryRef)query,
                                          (CFTypeRef*)&dataRef);
    if (status != errSecSuccess || !dataRef) {
        return NULL;
    }

    NSString* value = [[NSString alloc] initWithData:(__bridge_transfer NSData*)dataRef
                                            encoding:NSUTF8StringEncoding];
    // Unity expects a strdup'd C string it can free
    return strdup([value UTF8String]);
}
