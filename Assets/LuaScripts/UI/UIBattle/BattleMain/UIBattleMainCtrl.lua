--
-- author:dengxuhui
-- create date:2023/3/29
-- 战斗示例界面
-- @File-> Assets.LuaScripts.UI.UIBattle.BattleMain.UIBattleMainCtrl
-- @require-> local UIBattleMainCtrl = require "UI.UIBattle.BattleMain.UIBattleMainCtrl"

local UIBattleMainCtrl = BaseClass("UIBattleMainCtrl", UIBaseCtrl)

local function Back(self)
    SceneManager:I():SwitchScene(SceneConfig.HomeScene)
end

UIBattleMainCtrl.Back = Back

return UIBattleMainCtrl