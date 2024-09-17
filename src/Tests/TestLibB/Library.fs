namespace TestLibB

module Lib =
    let Value =
        #if LOCAL_SOURCE
            "LOCAL_OVERRIDE"
        #else
            "DEFAULT"
        #endif
