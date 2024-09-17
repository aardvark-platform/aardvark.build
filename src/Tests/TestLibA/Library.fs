namespace TestLibA

module Lib =

    #if LOCAL_SOURCE
    let LocalOverride = ()
    #endif

    let Value =
        #if LOCAL_SOURCE
            "LOCAL_OVERRIDE"
        #else
            "DEFAULT"
        #endif