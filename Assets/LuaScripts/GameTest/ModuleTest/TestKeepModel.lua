--[[
-- added by wsh @ 2018-02-26
--]]

local function Run()
	local target = UIManager:I():GetWindow(UIWindowNames.UILogin, true, true)
	if target then
		SceneManager:I():SwitchScene(SceneConfig.HomeScene)
	end
end

return {
	Run = Run
}