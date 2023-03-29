--
-- author:dengxuhui
-- create date:2023/3/29
-- 
-- @File-> Assets.LuaScripts.UI.UITestMain.TestMain.UITestMainCtrl
-- @require-> local UITestMainCtrl = require "UI.UITestMain.TestMain.UITestMainCtrl"

local UITestMainCtrl = BaseClass("UITestMainCtrl", UIBaseCtrl)

local function StartFighting(self)
	SceneManager:I():SwitchScene(SceneConfig.BattleScene)
end

local function Logout(self)
	SceneManager:I():SwitchScene(SceneConfig.LoginScene)
end

UITestMainCtrl.StartFighting = StartFighting
UITestMainCtrl.Logout = Logout

return UITestMainCtrl