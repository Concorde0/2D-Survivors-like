

function Test(...)

    local s = 0
    local arg = {...}
    for v in ipairs(arg) do
        s = s + v
    end
    print(#arg)
    return s/#arg
end
    print("aa",Test(1,2,3,4,5))
