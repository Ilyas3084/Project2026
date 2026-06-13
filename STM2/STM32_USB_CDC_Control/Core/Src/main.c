/* USER CODE BEGIN Header */
/**
  ******************************************************************************
  * @file           : main.c
  * @brief          : Main program body - USB CDC VCP для управления 10 SW + 2 KEY
  *                   Импульсный режим: 22 канала, по 2 направления на каждый SW
  ******************************************************************************
  */
/* USER CODE END Header */
/* Includes ------------------------------------------------------------------*/
#include "main.h"
#include "usb_device.h"
#include "usbd_cdc_if.h"
#include <stdio.h>
#include <string.h>
#include <stdlib.h>

/* Private define ------------------------------------------------------------*/
#define APP_RX_DATA_SIZE  64U
#define TOTAL_CHANNELS    22U
#define PULSE_MS          500U

/* Private variables ---------------------------------------------------------*/
extern volatile uint8_t usb_data_ready;
extern uint8_t usb_rx_buffer[APP_RX_DATA_SIZE];
extern uint32_t usb_rx_len;

/* Private function prototypes -----------------------------------------------*/
void SystemClock_Config(void);
static void MX_GPIO_Init(void);

typedef struct
{
  GPIO_TypeDef* port;
  uint16_t pin;
} ChannelMap;

static void Process_USB_Command(uint8_t* buf, uint32_t len);
static void Clear_All_Outputs(void);
static void Pulse_Channel(uint8_t channel);
static void Set_Channel_State(uint8_t channel, GPIO_PinState state);
static void Send_Status(void);

/* Карта 22 каналов:
   CH0  SW0 Dir1 -> PD15
   CH1  SW0 Dir2 -> PD14
   CH2  SW1 Dir1 -> PD13
   CH3  SW1 Dir2 -> PD12
   CH4  SW2 Dir1 -> PD11
   CH5  SW2 Dir2 -> PD10
   CH6  SW3 Dir1 -> PD9
   CH7  SW3 Dir2 -> PD8
   CH8  SW4 Dir1 -> PB15
   CH9  SW4 Dir2 -> PB14
   CH10 SW5 Dir1 -> PB13
   CH11 SW5 Dir2 -> PB12
   CH12 SW6 Dir1 -> PB11
   CH13 SW6 Dir2 -> PB10
   CH14 SW7 Dir1 -> PE15
   CH15 SW7 Dir2 -> PE14
   CH16 SW8 Dir1 -> PE13
   CH17 SW8 Dir2 -> PE12
   CH18 SW9 Dir1 -> PE11
   CH19 SW9 Dir2 -> PE10
   CH20 KEY0     -> PE9
   CH21 KEY1     -> PE7
*/
static const ChannelMap g_channel_map[TOTAL_CHANNELS] =
{
    { GPIOD, GPIO_PIN_15 }, { GPIOD, GPIO_PIN_14 },
    { GPIOD, GPIO_PIN_13 }, { GPIOD, GPIO_PIN_12 },
    { GPIOD, GPIO_PIN_11 }, { GPIOD, GPIO_PIN_10 },
    { GPIOD, GPIO_PIN_9  }, { GPIOD, GPIO_PIN_8  },

    { GPIOB, GPIO_PIN_15 }, { GPIOB, GPIO_PIN_14 },
    { GPIOB, GPIO_PIN_13 }, { GPIOB, GPIO_PIN_12 },
    { GPIOB, GPIO_PIN_11 }, { GPIOB, GPIO_PIN_10 },

    { GPIOE, GPIO_PIN_15 }, { GPIOE, GPIO_PIN_14 },
    { GPIOE, GPIO_PIN_13 }, { GPIOE, GPIO_PIN_12 },
    { GPIOE, GPIO_PIN_11 }, { GPIOE, GPIO_PIN_10 },
    { GPIOE, GPIO_PIN_9  }, { GPIOE, GPIO_PIN_7  }
};

int main(void)
{
  HAL_Init();
  SystemClock_Config();
  MX_GPIO_Init();
  MX_USB_DEVICE_Init();

  Clear_All_Outputs();
  HAL_Delay(100);

  CDC_Transmit_FS((uint8_t*)"STM32 CDC Ready\r\nSend channel 0-21 or ? or status\r\n", 51);

  while (1)
  {
    if (usb_data_ready)
    {
      usb_data_ready = 0;
      Process_USB_Command(usb_rx_buffer, usb_rx_len);
    }

    HAL_Delay(5);
  }
}

static void Process_USB_Command(uint8_t* buf, uint32_t len)
{
  char resp[64];
  long value = -1;
  char* endptr = NULL;

  if (len >= APP_RX_DATA_SIZE)
    len = APP_RX_DATA_SIZE - 1;

  buf[len] = '\0';

  while (len > 0 && (buf[len - 1] == '\r' || buf[len - 1] == '\n' || buf[len - 1] == ' '))
  {
    buf[len - 1] = '\0';
    len--;
  }

  while (*buf == ' ')
    buf++;

  if (*buf == '\0')
    return;

  if (strcmp((char*)buf, "?") == 0 || strcmp((char*)buf, "status") == 0)
  {
    Send_Status();
    return;
  }

  if (strcmp((char*)buf, "KEY0_ON") == 0)
  {
    Set_Channel_State(20, GPIO_PIN_SET);
    CDC_Transmit_FS((uint8_t*)"KEY0 ON\r\n", 9);
    return;
  }

  if (strcmp((char*)buf, "KEY0_OFF") == 0)
  {
    Set_Channel_State(20, GPIO_PIN_RESET);
    CDC_Transmit_FS((uint8_t*)"KEY0 OFF\r\n", 10);
    return;
  }

  if (strcmp((char*)buf, "KEY1_ON") == 0)
  {
    Set_Channel_State(21, GPIO_PIN_SET);
    CDC_Transmit_FS((uint8_t*)"KEY1 ON\r\n", 9);
    return;
  }

  if (strcmp((char*)buf, "KEY1_OFF") == 0)
  {
    Set_Channel_State(21, GPIO_PIN_RESET);
    CDC_Transmit_FS((uint8_t*)"KEY1 OFF\r\n", 10);
    return;
  }

  value = strtol((char*)buf, &endptr, 10);
  if (endptr != (char*)buf && *endptr == '\0')
  {
    if (value >= 0 && value < (long)TOTAL_CHANNELS)
    {
      Pulse_Channel((uint8_t)value);
      snprintf(resp, sizeof(resp), "PULSE %ld\r\n", value);
      CDC_Transmit_FS((uint8_t*)resp, (uint16_t)strlen(resp));
    }
    else
    {
      CDC_Transmit_FS((uint8_t*)"ERR: channel must be 0..21\r\n", 28);
    }
    return;
  }

  CDC_Transmit_FS((uint8_t*)"Send channel 0-21 or ? or status\r\n", 35);
}

static void Clear_All_Outputs(void)
{
  HAL_GPIO_WritePin(GPIOE,
      GPIO_PIN_7 | GPIO_PIN_8 | GPIO_PIN_9 | GPIO_PIN_10 |
      GPIO_PIN_11 | GPIO_PIN_12 | GPIO_PIN_13 | GPIO_PIN_14 | GPIO_PIN_15,
      GPIO_PIN_RESET);

  HAL_GPIO_WritePin(GPIOB,
      GPIO_PIN_10 | GPIO_PIN_11 | GPIO_PIN_12 |
      GPIO_PIN_13 | GPIO_PIN_14 | GPIO_PIN_15,
      GPIO_PIN_RESET);

  HAL_GPIO_WritePin(GPIOD,
      GPIO_PIN_8 | GPIO_PIN_9 | GPIO_PIN_10 | GPIO_PIN_11 |
      GPIO_PIN_12 | GPIO_PIN_13 | GPIO_PIN_14 | GPIO_PIN_15,
      GPIO_PIN_RESET);
}

static void Pulse_Channel(uint8_t channel)
{
  if (channel >= TOTAL_CHANNELS)
    return;

  Clear_All_Outputs();
  HAL_GPIO_WritePin(g_channel_map[channel].port, g_channel_map[channel].pin, GPIO_PIN_SET);
  HAL_Delay(PULSE_MS);
  HAL_GPIO_WritePin(g_channel_map[channel].port, g_channel_map[channel].pin, GPIO_PIN_RESET);
}

static void Set_Channel_State(uint8_t channel, GPIO_PinState state)
{
  if (channel >= TOTAL_CHANNELS)
    return;

  HAL_GPIO_WritePin(g_channel_map[channel].port, g_channel_map[channel].pin, state);
}

static void Send_Status(void)
{
  char resp[32];
  snprintf(resp, sizeof(resp), "STATE READY\r\n");
  CDC_Transmit_FS((uint8_t*)resp, (uint16_t)strlen(resp));
}

void SystemClock_Config(void)
{
  RCC_OscInitTypeDef RCC_OscInitStruct = {0};
  RCC_ClkInitTypeDef RCC_ClkInitStruct = {0};

  __HAL_RCC_PWR_CLK_ENABLE();
  __HAL_PWR_VOLTAGESCALING_CONFIG(PWR_REGULATOR_VOLTAGE_SCALE1);

  RCC_OscInitStruct.OscillatorType = RCC_OSCILLATORTYPE_HSI;
  RCC_OscInitStruct.HSIState = RCC_HSI_ON;
  RCC_OscInitStruct.HSICalibrationValue = RCC_HSICALIBRATION_DEFAULT;
  RCC_OscInitStruct.PLL.PLLState = RCC_PLL_ON;
  RCC_OscInitStruct.PLL.PLLSource = RCC_PLLSOURCE_HSI;
  RCC_OscInitStruct.PLL.PLLM = 16;
  RCC_OscInitStruct.PLL.PLLN = 192;
  RCC_OscInitStruct.PLL.PLLP = RCC_PLLP_DIV2;
  RCC_OscInitStruct.PLL.PLLQ = 4;
  if (HAL_RCC_OscConfig(&RCC_OscInitStruct) != HAL_OK)
  {
    Error_Handler();
  }

  RCC_ClkInitStruct.ClockType = RCC_CLOCKTYPE_HCLK | RCC_CLOCKTYPE_SYSCLK
                              | RCC_CLOCKTYPE_PCLK1 | RCC_CLOCKTYPE_PCLK2;
  RCC_ClkInitStruct.SYSCLKSource = RCC_SYSCLKSOURCE_HSI;
  RCC_ClkInitStruct.AHBCLKDivider = RCC_SYSCLK_DIV1;
  RCC_ClkInitStruct.APB1CLKDivider = RCC_HCLK_DIV1;
  RCC_ClkInitStruct.APB2CLKDivider = RCC_HCLK_DIV1;

  if (HAL_RCC_ClockConfig(&RCC_ClkInitStruct, FLASH_LATENCY_0) != HAL_OK)
  {
    Error_Handler();
  }
}

static void MX_GPIO_Init(void)
{
  GPIO_InitTypeDef GPIO_InitStruct = {0};

  __HAL_RCC_GPIOE_CLK_ENABLE();
  __HAL_RCC_GPIOB_CLK_ENABLE();
  __HAL_RCC_GPIOD_CLK_ENABLE();
  __HAL_RCC_GPIOA_CLK_ENABLE();

  HAL_GPIO_WritePin(GPIOE,
      GPIO_PIN_7 | GPIO_PIN_8 | GPIO_PIN_9 | GPIO_PIN_10 |
      GPIO_PIN_11 | GPIO_PIN_12 | GPIO_PIN_13 | GPIO_PIN_14 | GPIO_PIN_15,
      GPIO_PIN_RESET);

  HAL_GPIO_WritePin(GPIOB,
      GPIO_PIN_10 | GPIO_PIN_11 | GPIO_PIN_12 |
      GPIO_PIN_13 | GPIO_PIN_14 | GPIO_PIN_15,
      GPIO_PIN_RESET);

  HAL_GPIO_WritePin(GPIOD,
      GPIO_PIN_8 | GPIO_PIN_9 | GPIO_PIN_10 | GPIO_PIN_11 |
      GPIO_PIN_12 | GPIO_PIN_13 | GPIO_PIN_14 | GPIO_PIN_15,
      GPIO_PIN_RESET);

  GPIO_InitStruct.Mode = GPIO_MODE_OUTPUT_PP;
  GPIO_InitStruct.Pull = GPIO_NOPULL;
  GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_LOW;

  GPIO_InitStruct.Pin = GPIO_PIN_7 | GPIO_PIN_8 | GPIO_PIN_9 | GPIO_PIN_10
                      | GPIO_PIN_11 | GPIO_PIN_12 | GPIO_PIN_13 | GPIO_PIN_14
                      | GPIO_PIN_15;
  HAL_GPIO_Init(GPIOE, &GPIO_InitStruct);

  GPIO_InitStruct.Pin = GPIO_PIN_10 | GPIO_PIN_11 | GPIO_PIN_12
                      | GPIO_PIN_13 | GPIO_PIN_14 | GPIO_PIN_15;
  HAL_GPIO_Init(GPIOB, &GPIO_InitStruct);

  GPIO_InitStruct.Pin = GPIO_PIN_8 | GPIO_PIN_9 | GPIO_PIN_10 | GPIO_PIN_11
                      | GPIO_PIN_12 | GPIO_PIN_13 | GPIO_PIN_14 | GPIO_PIN_15;
  HAL_GPIO_Init(GPIOD, &GPIO_InitStruct);
}

void Error_Handler(void)
{
  __disable_irq();
  while (1)
  {
  }
}

#ifdef USE_FULL_ASSERT
void assert_failed(uint8_t *file, uint32_t line)
{
}
#endif
